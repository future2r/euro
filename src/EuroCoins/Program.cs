using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace EuroCoins;

/// <summary>
/// The entry point. Ensures that only one instance runs: a second start brings the existing
/// window to the front instead (both instances would otherwise write the same collection file).
/// </summary>
public static partial class Program
{
    /// <summary>The command-line argument passed when the app restarts itself.</summary>
    public const string RestartArgument = "--restarted";

    private const string InstanceKey = "EuroCoins.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (RedirectToRunningInstance(args.Contains(RestartArgument)))
        {
            return 0;
        }

        Application.Start(static callback =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static bool RedirectToRunningInstance(bool restarted)
    {
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);

        // After a restart the old process may still be shutting down; wait for it briefly.
        for (var attempt = 0; restarted && !instance.IsCurrent && attempt < 50; attempt++)
        {
            Thread.Sleep(100);
            instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        }

        if (instance.IsCurrent)
        {
            instance.Activated += (_, _) => App.Current?.BringToFront();
            return false;
        }

        // Redirect synchronously: the STA thread must not run an async continuation here.
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        using var done = new ManualResetEventSlim();
        Task.Run(async () =>
        {
            await instance.RedirectActivationToAsync(activation);
            done.Set();
        });
        done.Wait(TimeSpan.FromSeconds(5));
        return true;
    }

    /// <summary>Brings a window to the foreground (needed when activated from another process).</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    /// <summary>Returns the DPI of the monitor the window is on (96 = 100 %).</summary>
    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);
}
