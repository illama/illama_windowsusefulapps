using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : interrupteurs pour les réglages Windows les plus demandés,
/// plus la création d'un point de restauration système avant toute manipulation risquée.</summary>
public class TweaksService
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <summary>Diffuse WM_SETTINGCHANGE pour que l'Explorateur/les apps ouvertes réagissent tout de
    /// suite (thème, aperçu des icônes...) sans attendre une déconnexion. La valeur en registre, elle,
    /// est déjà persistée avant cet appel — cette notification est purement pour le retour visuel immédiat.</summary>
    private static void BroadcastSettingChange(string area)
    {
        try { SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, area, SMTO_ABORTIFHUNG, 2000, out _); }
        catch { /* purement cosmétique : jamais bloquant si ça échoue */ }
    }

    private static void RefreshExplorer()
    {
        try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
        catch { /* idem, cosmétique */ }
    }

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

    /// <summary>Comme WriteDword, mais n'interrompt pas les écritures suivantes en cas d'échec sur une
    /// clé (utile quand un réglage doit toucher plusieurs clés de secours) ; journalise l'échec au lieu
    /// de le laisser passer inaperçu.</summary>
    private static void WriteDwordBestEffort(RegistryHive hive, string path, string name, int value)
    {
        try { WriteDword(hive, path, name, value); }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Échec d'écriture registre {hive}\\{path}\\{name} : {ex.Message}", LogLevel.Warning);
        }
    }

    public List<QuickTweak> BuildTweaks()
    {
        const string explorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string search = @"Software\Microsoft\Windows\CurrentVersion\Search";
        const string contentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string gameDvr = @"System\GameConfigStore";
        const string gameDvrCurrentVersion = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        const string gameBar = @"Software\Microsoft\GameBar";
        const string gameDvrPolicy = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
        const string searchPolicy = @"SOFTWARE\Policies\Microsoft\Windows\Explorer";
        const string pushNotif = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
        const string telemetryPolicy = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

        return new List<QuickTweak>
        {
            new()
            {
                Nom = "Afficher les extensions de fichiers",
                Description = "Explorateur de fichiers : montre .exe, .txt, .docx, etc.",
                Getter = () => ReadDword(RegistryHive.CurrentUser, explorerAdv, "HideFileExt", 1) == 0,
                Setter = on => { WriteDword(RegistryHive.CurrentUser, explorerAdv, "HideFileExt", on ? 0 : 1); RefreshExplorer(); },
            },
            new()
            {
                Nom = "Afficher les fichiers et dossiers cachés",
                Description = "Explorateur de fichiers : montre les éléments masqués",
                Getter = () => ReadDword(RegistryHive.CurrentUser, explorerAdv, "Hidden", 2) == 1,
                Setter = on => { WriteDword(RegistryHive.CurrentUser, explorerAdv, "Hidden", on ? 1 : 2); RefreshExplorer(); },
            },
            new()
            {
                Nom = "Thème sombre (applications)",
                Description = "Force le mode sombre pour les applications Windows",
                Getter = () => ReadDword(RegistryHive.CurrentUser, personalize, "AppsUseLightTheme", 1) == 0,
                Setter = on => { WriteDword(RegistryHive.CurrentUser, personalize, "AppsUseLightTheme", on ? 0 : 1); BroadcastSettingChange("ImmersiveColorSet"); },
            },
            new()
            {
                Nom = "Thème sombre (système)",
                Description = "Force le mode sombre pour la barre des tâches et le menu Démarrer",
                Getter = () => ReadDword(RegistryHive.CurrentUser, personalize, "SystemUsesLightTheme", 1) == 0,
                Setter = on => { WriteDword(RegistryHive.CurrentUser, personalize, "SystemUsesLightTheme", on ? 0 : 1); BroadcastSettingChange("ImmersiveColorSet"); },
            },
            new()
            {
                Nom = "Désactiver la recherche Bing (menu Démarrer)",
                Description = "Empêche les résultats web de polluer la recherche locale",
                // Certaines versions de Windows 11 ignorent BingSearchEnabled seul : on ajoute la
                // stratégie DisableSearchBoxSuggestions (plus autoritaire) pour que ça tienne vraiment.
                Getter = () => ReadDword(RegistryHive.CurrentUser, search, "BingSearchEnabled", 1) == 0,
                Setter = on =>
                {
                    WriteDword(RegistryHive.CurrentUser, search, "BingSearchEnabled", on ? 0 : 1);
                    WriteDwordBestEffort(RegistryHive.CurrentUser, searchPolicy, "DisableSearchBoxSuggestions", on ? 1 : 0);
                },
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
                Description = "Coupe l'enregistrement de jeu en arrière-plan et l'overlay Game Bar",
                // Le simple GameDVR_Enabled ne suffit pas : c'est AllowGameDVR (stratégie, HKLM) qui
                // fait vraiment apparaître le bouton "Xbox Game Bar" comme désactivé dans les Paramètres
                // Windows et qui survit au redémarrage de façon fiable.
                Getter = () => ReadDword(RegistryHive.LocalMachine, gameDvrPolicy, "AllowGameDVR", 1) == 0,
                Setter = on =>
                {
                    int v = on ? 0 : 1;
                    // Best-effort sur chaque clé : si l'une échoue (permissions, build Windows différent...),
                    // les autres sont quand même tentées au lieu de tout annuler au premier échec.
                    WriteDwordBestEffort(RegistryHive.CurrentUser, gameDvr, "GameDVR_Enabled", v);
                    WriteDwordBestEffort(RegistryHive.CurrentUser, gameDvrCurrentVersion, "AppCaptureEnabled", v);
                    WriteDwordBestEffort(RegistryHive.CurrentUser, gameBar, "UseNexusForGameBarEnabled", on ? 0 : 1);
                    WriteDword(RegistryHive.LocalMachine, gameDvrPolicy, "AllowGameDVR", v); // clé principale : l'échec ici doit remonter
                },
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
