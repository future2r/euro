using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EuroCoins.Core.Collections;

/// <summary>
/// Keeps the <see cref="CollectionState"/> in sync with its JSON file: every change is saved
/// immediately (atomically), and changes made by another device (e.g. through OneDrive) are
/// picked up automatically. When both sides changed, the file that was saved last wins.
/// </summary>
public sealed partial class CollectionStore : IDisposable
{
    private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(500);

    private readonly CollectionState collection;
    private readonly ILogger<CollectionStore> logger;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly Lock hashLock = new();
    private SynchronizationContext? context;
    private FileSystemWatcher? watcher;
    private Timer? reloadTimer;
    private string? lastKnownHash;
    private (string Path, string Json, long Version)? pending;
    private long writtenVersion;
    private bool disposed;

    /// <summary>
    /// Creates a store for the given collection.
    /// </summary>
    /// <param name="collection">The collection to keep in sync.</param>
    /// <param name="logger">The logger.</param>
    public CollectionStore(CollectionState collection, ILogger<CollectionStore> logger)
    {
        this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        collection.EntryChanged += (_, _) => Save();
    }

    /// <summary>
    /// Occurs when saving failed, or when the collection changed although no file is open (e.g. after
    /// the file could not be loaded). Raised on the synchronization context of the thread that called
    /// <see cref="Open"/>.
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>
    /// Gets the path of the collection file, or <see langword="null"/> before <see cref="Open"/>.
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Opens a collection file: loads it (a missing file means an empty collection) and starts
    /// watching it for external changes. Events are raised on the current synchronization context.
    /// </summary>
    /// <param name="path">The path of the collection file.</param>
    /// <exception cref="InvalidDataException">The file exists but cannot be read.</exception>
    public void Open(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Parse before switching files, so that an unreadable file is never overwritten.
        var fullPath = Path.GetFullPath(path);
        var json = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : null;
        var entries = json is null ? [] : CollectionDocument.Deserialize(json).ToEntries().ToList();

        Flush(); // Finish writing the previous file first.
        StopWatching();
        context = SynchronizationContext.Current;
        FilePath = fullPath;
        collection.ReplaceAll(entries);
        SetLastKnownHash(json);
        StartWatching();
    }

    /// <summary>
    /// Writes a pending change synchronously, e.g. before the app exits or restarts.
    /// </summary>
    public void Flush()
    {
        if (!disposed)
        {
            // Safe to block: WritePendingAsync never resumes on the caller's synchronization context.
            WritePendingAsync().GetAwaiter().GetResult();
        }
    }

    private async Task WritePendingAsync()
    {
        await writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            (string Path, string Json, long Version) snapshot;
            lock (hashLock)
            {
                if (pending is not { } next || next.Version <= writtenVersion)
                {
                    return; // A later call already wrote this (or a newer) state.
                }

                snapshot = next;
            }

            try
            {
                await Task.Run(() => AtomicFile.WriteAllText(snapshot.Path, snapshot.Json)).ConfigureAwait(false);
                lock (hashLock)
                {
                    writtenVersion = snapshot.Version;
                    lastKnownHash = ComputeHash(snapshot.Json);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogSaveFailed(ex, snapshot.Path);
                Raise(() => SaveFailed?.Invoke(this, ex));
            }
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopWatching();
        writeLock.Dispose();
    }

    private static string ComputeHash(string? json) =>
        json is null ? string.Empty : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    // Called on CollectionState.EntryChanged; ReplaceAll (open, reload) is not saved because it reflects the file.
    private void Save()
    {
        if (disposed)
        {
            return;
        }

        if (FilePath is not { } path)
        {
            var notOpen = new InvalidOperationException("The collection file is not open; changes are not saved.");
            Raise(() => SaveFailed?.Invoke(this, notOpen));
            return;
        }

        // Serialize on the caller's thread (the collection is not thread-safe); the newest snapshot
        // replaces any snapshot that is still waiting, so writes can never overtake each other.
        var json = CollectionDocument.Serialize(collection);
        lock (hashLock)
        {
            pending = (path, json, (pending?.Version ?? writtenVersion) + 1);
        }

        _ = WritePendingAsync();
    }

    private void SetLastKnownHash(string? json)
    {
        lock (hashLock)
        {
            lastKnownHash = ComputeHash(json);
        }
    }

    private void StartWatching()
    {
        // Create the folder now (first start), otherwise the watcher could not be set up.
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        reloadTimer = new Timer(_ => ReloadIfChanged(), null, Timeout.Infinite, Timeout.Infinite);
        watcher = new FileSystemWatcher(directory, Path.GetFileName(FilePath)!)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += OnFileEvent;
        watcher.Created += OnFileEvent;
        watcher.Renamed += OnFileEvent;
        watcher.EnableRaisingEvents = true;
    }

    private void StopWatching()
    {
        watcher?.Dispose();
        watcher = null;
        reloadTimer?.Dispose();
        reloadTimer = null;
    }

    // File events come in bursts (OneDrive, atomic replace); reload once after they settle.
    private void OnFileEvent(object sender, FileSystemEventArgs e) =>
        reloadTimer?.Change(ReloadDelay, Timeout.InfiniteTimeSpan);

    private void ReloadIfChanged()
    {
        if (FilePath is not { } path || disposed || !File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var hash = ComputeHash(json);
            lock (hashLock)
            {
                if (hash == lastKnownHash)
                {
                    return; // Our own write.
                }

                lastKnownHash = hash;
            }

            var entries = CollectionDocument.Deserialize(json).ToEntries().ToList();
            Raise(() => collection.ReplaceAll(entries)); // Raises CollectionState.Reset.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The file may be locked or half-written while it syncs; the next event retries.
            LogReloadFailed(ex, path);
        }
    }

    private void Raise(Action action)
    {
        if (context is null)
        {
            action();
        }
        else
        {
            context.Post(_ => action(), null);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Saving the collection to {Path} failed.")]
    private partial void LogSaveFailed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reloading the collection from {Path} failed.")]
    private partial void LogReloadFailed(Exception exception, string path);
}
