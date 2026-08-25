namespace SystemManagerPro.Services;

public record AppCatalogEntry(string WingetId, string Name, string Category, string Description, string Icon);

/// <summary>Nouvelle fonctionnalité : installe en un clic une sélection d'applications courantes
/// (navigateurs, launchers de jeux, utilitaires...) via winget, déjà présent sur Windows 10/11.</summary>
public class AppInstallerService
{
    public static readonly List<AppCatalogEntry> Catalog = new()
    {
        // ----- Navigateurs -----
        new("Google.Chrome", "Google Chrome", "Navigateurs", "Le navigateur le plus utilisé au monde", "🌐"),
        new("Mozilla.Firefox", "Mozilla Firefox", "Navigateurs", "Rapide, respectueux de la vie privée", "🦊"),
        new("Brave.Brave", "Brave", "Navigateurs", "Bloqueur de pub intégré", "🦁"),
        new("Opera.OperaGX", "Opera GX", "Navigateurs", "Navigateur pensé pour les gamers", "🎮"),
        new("VivaldiTechnologies.Vivaldi", "Vivaldi", "Navigateurs", "Très personnalisable", "🎨"),

        // ----- Jeux & Launchers -----
        new("Valve.Steam", "Steam", "Jeux & Launchers", "La plus grande plateforme de jeux PC", "🎮"),
        new("EpicGames.EpicGamesLauncher", "Epic Games Launcher", "Jeux & Launchers", "Fortnite, jeux gratuits chaque semaine", "🎮"),
        new("ElectronicArts.EADesktop", "EA App", "Jeux & Launchers", "Battlefield, FIFA, Apex Legends...", "🎮"),
        new("Blizzard.BattleNet", "Battle.net", "Jeux & Launchers", "Overwatch, Diablo, World of Warcraft...", "🎮"),
        new("Ubisoft.Connect", "Ubisoft Connect", "Jeux & Launchers", "Assassin's Creed, Rainbow Six...", "🎮"),
        new("GOG.Galaxy", "GOG Galaxy", "Jeux & Launchers", "Jeux sans DRM", "🎮"),
        new("Discord.Discord", "Discord", "Jeux & Launchers", "Chat vocal/texte pour gamers", "💬"),
        new("OBSProject.OBSStudio", "OBS Studio", "Jeux & Launchers", "Enregistrement et streaming", "🎥"),
        new("Guru3D.Afterburner", "MSI Afterburner", "Jeux & Launchers", "Overclocking et monitoring FPS", "📈"),

        // ----- Communication -----
        new("Zoom.Zoom", "Zoom", "Communication", "Visioconférence", "📹"),
        new("Microsoft.Teams", "Microsoft Teams", "Communication", "Chat et visio professionnels", "👥"),

        // ----- Utilitaires -----
        new("7zip.7zip", "7-Zip", "Utilitaires", "Archiveur gratuit", "🗜️"),
        new("RARLab.WinRAR", "WinRAR", "Utilitaires", "Archiveur populaire", "🗜️"),
        new("Notepad++.Notepad++", "Notepad++", "Utilitaires", "Éditeur de texte pour développeurs", "📝"),
        new("Microsoft.PowerToys", "PowerToys", "Utilitaires", "Utilitaires Windows officiels de Microsoft", "🛠️"),
        new("voidtools.Everything", "Everything", "Utilitaires", "Recherche de fichiers instantanée", "🔍"),

        // ----- Multimédia -----
        new("VideoLAN.VLC", "VLC Media Player", "Multimédia", "Lecteur multimédia universel", "🎬"),
        new("Spotify.Spotify", "Spotify", "Multimédia", "Musique en streaming", "🎵"),

        // ----- Développement -----
        new("Microsoft.VisualStudioCode", "Visual Studio Code", "Développement", "Éditeur de code de Microsoft", "💻"),
        new("Git.Git", "Git", "Développement", "Gestionnaire de versions", "🔧"),
    };

    public bool IsWingetAvailable() => ProcessRunner.Run("winget", "--version").ExitCode == 0;

    /// <summary>Vérifie en parallèle lesquels des paquets du catalogue sont déjà installés.</summary>
    public async Task<HashSet<string>> GetInstalledIdsAsync()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checks = Catalog.Select(app => Task.Run(() =>
        {
            var r = ProcessRunner.Run("winget",
                $"list --id {app.WingetId} -e --accept-source-agreements", timeoutMs: 20000);
            return (app.WingetId, Found: r.ExitCode == 0 && r.StdOut.Contains(app.WingetId, StringComparison.OrdinalIgnoreCase));
        })).ToList();

        var results = await Task.WhenAll(checks);
        foreach (var (id, found) in results)
            if (found) installed.Add(id);

        return installed;
    }

    public Task<(bool Ok, string Message)> InstallAsync(string wingetId) => Task.Run(() =>
    {
        var result = ProcessRunner.Run("winget",
            $"install --id {wingetId} -e --silent --accept-package-agreements --accept-source-agreements",
            timeoutMs: 600000); // certains launchers (EA App, Battle.net...) sont volumineux : jusqu'à 10 min

        return result.ExitCode == 0
            ? (true, "Installé avec succès.")
            : (false, $"Échec (code {result.ExitCode}). {Truncate(result.StdErr.Length > 0 ? result.StdErr : result.StdOut)}");
    });

    private static string Truncate(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
