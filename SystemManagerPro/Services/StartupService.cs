using System.IO;
using Microsoft.Win32;
using SystemManagerPro.Models;
using TaskScheduler = Microsoft.Win32.TaskScheduler.TaskService;

namespace SystemManagerPro.Services;

/// <summary>Gère les applications qui démarrent avec Windows : clés de registre Run,
/// tâches planifiées avec déclencheur "à l'ouverture de session" et dossier Démarrage.</summary>
public class StartupService
{
    private static readonly (RegistryHive Hive, string Path)[] RunKeys =
    {
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
    };

    public List<StartupApp> GetAll()
    {
        var apps = new List<StartupApp>();

        foreach (var (hive, path) in RunKeys)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(path);
            if (key == null) continue;
            var hiveLabel = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
            foreach (var name in key.GetValueNames())
            {
                if (name.StartsWith("PS", StringComparison.OrdinalIgnoreCase)) continue;
                apps.Add(new StartupApp
                {
                    Nom = name,
                    Chemin = key.GetValue(name)?.ToString() ?? "",
                    Type = StartupSourceType.Registry,
                    Emplacement = $"{hiveLabel}\\{path}",
                    Actif = true,
                });
            }
        }

        try
        {
            using var ts = new TaskScheduler();
            foreach (var task in EnumerateTasks(ts.RootFolder))
            {
                bool hasLogonTrigger = task.Definition.Triggers.Any(t =>
                    t.TriggerType == Microsoft.Win32.TaskScheduler.TaskTriggerType.Logon);
                if (!hasLogonTrigger) continue;

                string exe = task.Definition.Actions.OfType<Microsoft.Win32.TaskScheduler.ExecAction>()
                    .FirstOrDefault()?.Path ?? "(action non-exécutable)";

                apps.Add(new StartupApp
                {
                    Nom = task.Name,
                    Chemin = exe,
                    Type = StartupSourceType.ScheduledTask,
                    Emplacement = task.Path,
                    Actif = task.Enabled,
                });
            }
        }
        catch { /* Le service de planification n'est peut-être pas accessible */ }

        foreach (var folder in StartupFolders())
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                bool disabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                apps.Add(new StartupApp
                {
                    Nom = Path.GetFileName(disabled ? file[..^9] : file),
                    Chemin = file,
                    Type = StartupSourceType.StartupFolder,
                    Emplacement = folder,
                    Actif = !disabled,
                });
            }
        }

        return apps.OrderBy(a => a.Nom, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<Microsoft.Win32.TaskScheduler.Task> EnumerateTasks(Microsoft.Win32.TaskScheduler.TaskFolder folder)
    {
        foreach (var t in folder.Tasks) yield return t;
        foreach (var sub in folder.SubFolders)
            foreach (var t in EnumerateTasks(sub)) yield return t;
    }

    private static IEnumerable<string> StartupFolders()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
    }

    public void Add(string name, string path)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        key.SetValue(name, $"\"{path}\"");
    }

    public void SetEnabled(StartupApp app, bool enabled)
    {
        switch (app.Type)
        {
            case StartupSourceType.Registry:
                if (!enabled)
                {
                    var (hiveKey, subPath) = SplitEmplacement(app.Emplacement);
                    using (var key = hiveKey.OpenSubKey(subPath, writable: true))
                        key?.DeleteValue(app.Nom, throwOnMissingValue: false);
                }
                break;

            case StartupSourceType.ScheduledTask:
                using (var ts = new TaskScheduler())
                {
                    // app.Emplacement contient déjà le chemin complet de la tâche (dossier + nom).
                    var task = ts.GetTask(app.Emplacement);
                    if (task != null) task.Enabled = enabled;
                }
                break;

            case StartupSourceType.StartupFolder:
                if (enabled && app.Chemin.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    var target = app.Chemin[..^9];
                    File.Move(app.Chemin, target, overwrite: true);
                    app.Chemin = target;
                }
                else if (!enabled && !app.Chemin.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    var target = app.Chemin + ".disabled";
                    File.Move(app.Chemin, target, overwrite: true);
                    app.Chemin = target;
                }
                break;
        }
        app.Actif = enabled;
    }

    private static (RegistryKey Hive, string Path) SplitEmplacement(string emplacement)
    {
        var parts = emplacement.Split('\\', 2);
        var hive = parts[0] == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        return (hive, parts.Length > 1 ? parts[1] : "");
    }
}
