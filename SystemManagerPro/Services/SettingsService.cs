using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Préférences persistantes de l'application (démarrage avec Windows, démarrage minimisé,
/// réduction dans la barre d'état système) — stockées en JSON dans le profil utilisateur.</summary>
public class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private const string RunValueName = "GestionnaireSystemePro";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnaireSystemePro");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Current { get; private set; }

    private SettingsService()
    {
        Current = Load();
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

    /// <summary>Active/désactive le lancement automatique au démarrage de Windows (clé Registre Run).</summary>
    public void SetStartWithWindows(bool enabled)
    {
        Current.StartWithWindows = enabled;
        SyncStartupRegistration();
        Save();
    }

    public void SetStartMinimized(bool enabled)
    {
        Current.StartMinimized = enabled;
        SyncStartupRegistration(); // met à jour l'argument --minimized de la clé Run si elle existe
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
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (Current.StartWithWindows)
        {
            string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            string args = Current.StartMinimized ? " --minimized" : "";
            key.SetValue(RunValueName, $"\"{exePath}\"{args}");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Vérifie l'état réel de la clé Registre (au cas où elle aurait été modifiée hors de l'appli).</summary>
    public bool IsStartWithWindowsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) != null;
    }
}
