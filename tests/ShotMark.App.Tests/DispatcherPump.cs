using System.IO;
using System.Windows.Threading;

namespace ShotMark.App.Tests;

/// <summary>Lässt WPF-Dispatcher-Arbeit (Timer, await-Fortsetzungen) im Test-Thread laufen.</summary>
internal static class DispatcherPump
{
    public static void Install()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        // Fehlerprotokoll der Tests nicht in %LOCALAPPDATA%\ShotMark\log.txt des Nutzers schreiben
        Log.FilePath = Path.Combine(Path.GetTempPath(), "shotmark-tests", "log.txt");
    }

    public static void Run(Func<Task> action) => Run(async () =>
    {
        await action();
        return true;
    });

    public static T Run<T>(Func<Task<T>> action)
    {
        var task = action();
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < until) Pump(20);
        return task.GetAwaiter().GetResult();
    }

    public static void Pump(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        do
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(5);
        } while (DateTime.UtcNow < until);
    }
}
