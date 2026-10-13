using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EuroCoins.Core.Logging;

/// <summary>
/// A minimal logger provider that writes to rotating log files
/// (<c>EuroCoins.log</c>, <c>EuroCoins.1.log</c>, …). Fail-safe: logging never throws.
/// </summary>
/// <param name="directory">The log folder.</param>
public sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private const string BaseName = "EuroCoins";
    private const LogLevel MinimumLevel = LogLevel.Warning;
    private const long MaxFileSize = 1024 * 1024;
    private const int MaxFiles = 3; // Including the current file.
    private readonly Lock writeLock = new();

    private string CurrentFile => Path.Combine(directory, BaseName + ".log");

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append(" [").Append(level).Append("] ")
            .Append(category).Append(": ").Append(message)
            .AppendLine();
        if (exception is not null)
        {
            line.AppendLine(exception.ToString());
        }

        lock (writeLock)
        {
            try
            {
                Directory.CreateDirectory(directory);
                RotateIfNeeded();
                File.AppendAllText(CurrentFile, line.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never crash the app.
            }
        }
    }

    private void RotateIfNeeded()
    {
        var current = new FileInfo(CurrentFile);
        if (!current.Exists || current.Length < MaxFileSize)
        {
            return;
        }

        for (var i = MaxFiles - 1; i >= 1; i--)
        {
            var source = i == 1 ? CurrentFile : Path.Combine(directory, $"{BaseName}.{i - 1}.log");
            var target = Path.Combine(directory, $"{BaseName}.{i}.log");
            if (File.Exists(source))
            {
                File.Move(source, target, overwrite: true);
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
