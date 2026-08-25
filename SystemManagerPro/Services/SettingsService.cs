using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SystemManagerPro.Models;
using TS = Microsoft.Win32.TaskScheduler;

namespace SystemManagerPro.Services;

/// <summary>Préférences persistantes de l'application (démarrage avec Windows, démarrage minimisé,
/// réduction dans la barre d'état système) — stockées en JSON dans le profil utilisateur.</summary>
public class SettingsService
{
    public static SettingsService Instance { get; } = new();

    // Ancienne méthode (clé Run) — conservée uniquement pour nettoyer les installations précédentes.
    private const string RunValueName = "GestionnaireSystemePro";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Nouvelle méthode : tâche planifiée "Exécuter avec les autorisations maximales". Contrairement à la
    // clé Run, une tâche planifiée peut démarrer un programme qui exige l'élévation (notre manifeste
    // requireAdministrator) SANS invite UAC à chaque ouverture de session — la clé Run en est incapable
    // et échoue silencieusement pour ce genre de programme, ce qui explique que "coché" ne suffisait pas.
    private const string TaskName = "GestionnaireSystemePro_Autostart";

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnaireSystemePro");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Current { get; private set; }

    private SettingsService()
    {
        Current = Load();

        // Auto-réparation : si l'utilisateur a coché "Démarrer avec Windows" avec une version antérieure
        // (qui utilisait une clé Registre Run, incapable de lancer un programme élevé sans invite UAC),
        // on migre silencieusement vers la tâche planifiée dès l'ouverture de l'appli, sans action requise.
        if (Current.StartWithWindows)
        {
            try { SyncStartupRegistration(); }
            catch { /* pas bloquant au démarrage ; l'utilisateur peut re-basculer le réglage manuellement */ }
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { /* fichier corrompu ou illisible : on repart sur des valeurs par défaut */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* pas bloquant si l'écriture échoue */ }
    }

    /// <summary>Active/désactive le lancement automatique au démarrage de Windows (tâche planifiée élevée).</summary>
    public void SetStartWithWindows(bool enabled)
    {
        Current.StartWithWindows = enabled;
        SyncStartupRegistration();
        Save();
    }

    public void SetStartMinimized(bool enabled)
    {
        Current.StartMinimized = enabled;
        SyncStartupRegistration(); // met à jour l'argument --minimized de la tâche planifiée si elle existe
        Save();
    }

    public void SetCloseToTray(bool enabled)
    {
        Current.CloseToTray = enabled;
        Save();
    }

    public void SetCheckUpdatesOnStartup(bool enabled)
    {
        Current.CheckUpdatesOnStartup = enabled;
        Save();
    }

    private void SyncStartupRegistration()
    {
        // Nettoie l'ancienne clé Run d'une éventuelle version précédente : en plus de ne pas fonctionner
        // pour un programme élevé, la laisser traîner ferait tenter (et échouer) un double lancement.
        using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);

        using var ts = new TS.TaskService();

        if (Current.StartWithWindows)
        {
            string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            string args = Current.StartMinimized ? "--minimized" : "";

            string qualifiedUser = $@"{Environment.UserDomainName}\{Environment.UserName}";

            var def = ts.NewTask();
            def.RegistrationInfo.Description = "Lance Gestionnaire Système Pro à l'ouverture de session.";
            def.Principal.UserId = qualifiedUser;
            def.Principal.LogonType = TS.TaskLogonType.InteractiveToken;
            def.Principal.RunLevel = TS.TaskRunLevel.Highest; // démarrage élevé sans invite UAC à la connexion
            def.Settings.DisallowStartIfOnBatteries = false;
            def.Settings.StopIfGoingOnBatteries = false;
            def.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            def.Settings.StartWhenAvailable = true;

            var trigger = new TS.LogonTrigger { UserId = qualifiedUser };
            def.Triggers.Add(trigger);
            def.Actions.Add(new TS.ExecAction(exePath, args, null));

            ts.RootFolder.RegisterTaskDefinition(TaskName, def);
        }
        else
        {
            try { ts.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false); }
            catch { /* déjà absente : rien à faire */ }
        }
    }

    /// <summary>Vérifie l'état réel de la tâche planifiée (au cas où elle aurait été modifiée hors de l'appli).</summary>
    public bool IsStartWithWindowsRegistered()
    {
        using var ts = new TS.TaskService();
        return ts.GetTask(TaskName) != null;
    }
}
