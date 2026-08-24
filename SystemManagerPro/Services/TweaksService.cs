using System.Management;
using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : interrupteurs pour les réglages Windows les plus demandés,
/// plus la création d'un point de restauration système avant toute manipulation risquée.</summary>
public class TweaksService
{
    private static int ReadDword(RegistryHive hive, string path, string name, int fallback)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(path);
        var val = key?.GetValue(name);
        return val != null ? Convert.ToInt32(val) : fallback;
    }

    private static void WriteDword(RegistryHive hive, string path, string name, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(path, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    public List<QuickTweak> BuildTweaks()
    {
        const string explorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string search = @"Software\Microsoft\Windows\CurrentVersion\Search";
        const string contentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string gameDvr = @"System\GameConfigStore";
        const string pushNotif = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
        const string telemetryPolicy = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

        return new List<QuickTweak>
        {
            new()
            {
                Nom = "Afficher les extensions de fichiers",
                Description = "Explorateur de fichiers : montre .exe, .txt, .docx, etc.",
                Getter = () => ReadDword(RegistryHive.CurrentUser, explorerAdv, "HideFileExt", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, explorerAdv, "HideFileExt", on ? 0 : 1),
            },
            new()
            {
                Nom = "Afficher les fichiers et dossiers cachés",
                Description = "Explorateur de fichiers : montre les éléments masqués",
                Getter = () => ReadDword(RegistryHive.CurrentUser, explorerAdv, "Hidden", 2) == 1,
                Setter = on => WriteDword(RegistryHive.CurrentUser, explorerAdv, "Hidden", on ? 1 : 2),
            },
            new()
            {
                Nom = "Thème sombre (applications)",
                Description = "Force le mode sombre pour les applications Windows",
                Getter = () => ReadDword(RegistryHive.CurrentUser, personalize, "AppsUseLightTheme", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, personalize, "AppsUseLightTheme", on ? 0 : 1),
            },
            new()
            {
                Nom = "Thème sombre (système)",
                Description = "Force le mode sombre pour la barre des tâches et le menu Démarrer",
                Getter = () => ReadDword(RegistryHive.CurrentUser, personalize, "SystemUsesLightTheme", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, personalize, "SystemUsesLightTheme", on ? 0 : 1),
            },
            new()
            {
                Nom = "Désactiver la recherche Bing (menu Démarrer)",
                Description = "Empêche les résultats web de polluer la recherche locale",
                Getter = () => ReadDword(RegistryHive.CurrentUser, search, "BingSearchEnabled", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, search, "BingSearchEnabled", on ? 0 : 1),
            },
            new()
            {
                Nom = "Désactiver les suggestions et publicités",
                Description = "Retire les suggestions d'apps dans le menu Démarrer",
                Getter = () => ReadDword(RegistryHive.CurrentUser, contentDelivery, "SubscribedContent-338388Enabled", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, contentDelivery, "SubscribedContent-338388Enabled", on ? 0 : 1),
            },
            new()
            {
                Nom = "Désactiver la Xbox Game Bar",
                Description = "Coupe l'enregistrement de jeu en arrière-plan (Game DVR)",
                Getter = () => ReadDword(RegistryHive.CurrentUser, gameDvr, "GameDVR_Enabled", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, gameDvr, "GameDVR_Enabled", on ? 0 : 1),
            },
            new()
            {
                Nom = "Désactiver les notifications (Centre de notifications)",
                Description = "Coupe les bulles de notification (toasts)",
                Getter = () => ReadDword(RegistryHive.CurrentUser, pushNotif, "ToastEnabled", 1) == 0,
                Setter = on => WriteDword(RegistryHive.CurrentUser, pushNotif, "ToastEnabled", on ? 0 : 1),
            },
            new()
            {
                Nom = "Désactiver la télémétrie Windows",
                Description = "Stratégie AllowTelemetry = 0 (niveau minimal)",
                Getter = () => ReadDword(RegistryHive.LocalMachine, telemetryPolicy, "AllowTelemetry", 1) == 0,
                Setter = on => WriteDword(RegistryHive.LocalMachine, telemetryPolicy, "AllowTelemetry", on ? 0 : 1),
            },
        };
    }

    public (bool Ok, string Message) CreateRestorePoint(string description)
    {
        try
        {
            using var mc = new ManagementClass(@"\\.\root\default:SystemRestore");
            using var inParams = mc.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            inParams["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
            using var outParams = mc.InvokeMethod("CreateRestorePoint", inParams, null);
            var ret = Convert.ToUInt32(outParams?["ReturnValue"] ?? 1u);
            return ret == 0
                ? (true, "Point de restauration créé avec succès.")
                : (false, $"Échec (code {ret}). La protection du système est peut-être désactivée sur ce disque.");
        }
        catch (Exception ex)
        {
            return (false, "Erreur : " + ex.Message);
        }
    }
}
