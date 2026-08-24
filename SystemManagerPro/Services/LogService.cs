using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Journal d'activité central de l'application (singleton), affiché dans l'onglet Journal
/// et alimenté par tous les autres services au fur et à mesure des actions effectuées.</summary>
public sealed class LogService
{
    public static LogService Instance { get; } = new();
    public ObservableCollection<LogEntry> Entries { get; } = new();

    private LogService() { }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry { Message = message, Level = level };
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => Entries.Insert(0, entry));
        }
        else
        {
            Entries.Insert(0, entry);
        }
    }

    public void Clear() => Entries.Clear();

    public void ExportTo(string path)
    {
        var lines = Entries
            .OrderBy(e => e.Timestamp)
            .Select(e => $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.Level}] {e.Message}");
        File.WriteAllLines(path, lines);
    }
}
