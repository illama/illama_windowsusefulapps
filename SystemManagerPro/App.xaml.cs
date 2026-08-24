using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SystemManagerPro;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "crash-log.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            HandleException(args.Exception, "DispatcherUnhandledException");
            args.Handled = true; // évite un plantage silencieux — on garde la fenêtre principale ouverte si possible
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex) HandleException(ex, "AppDomain.UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            HandleException(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private static void HandleException(Exception ex, string source)
    {
        string full = Unwrap(ex);

        try
        {
            File.AppendAllText(LogPath,
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {source} =====\n{full}\n\n");
        }
        catch { /* si on ne peut même pas écrire le journal, tant pis */ }

        MessageBox.Show(
            "Une erreur inattendue est survenue :\n\n" + full +
            $"\n\nDétails enregistrés dans :\n{LogPath}",
            "Gestionnaire Système Pro — Erreur",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string Unwrap(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current != null)
        {
            parts.Add($"{current.GetType().FullName}: {current.Message}\n{current.StackTrace}");
            current = current.InnerException;
        }
        return string.Join("\n---- Cause interne ----\n", parts);
    }
}
