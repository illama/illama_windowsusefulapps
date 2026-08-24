using System.IO;
using System.Runtime.InteropServices;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : scanne et nettoie les fichiers temporaires,
/// caches navigateurs, corbeille et cache Windows Update pour libérer de l'espace disque.</summary>
public class CleanupService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public List<CleanupCategory> BuildCategories()
    {
        string temp = Path.GetTempPath();
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var categories = new List<CleanupCategory>
        {
            new()
            {
                Nom = "Fichiers temporaires utilisateur",
                Description = "Dossier %TEMP% de votre profil",
                Chemins = { temp },
            },
            new()
            {
                Nom = "Fichiers temporaires Windows",
                Description = "C:\\Windows\\Temp — nécessite les droits admin",
                Chemins = { Path.Combine(windir, "Temp") },
            },
            new()
            {
                Nom = "Cache Windows Update",
                Description = "Fichiers de mise à jour déjà installés, régénérés au besoin",
                Chemins = { Path.Combine(windir, "SoftwareDistribution", "Download") },
            },
            new()
            {
                Nom = "Prefetch",
                Description = "Cache de préchargement Windows",
                Chemins = { Path.Combine(windir, "Prefetch") },
            },
            new()
            {
                Nom = "Cache Microsoft Edge",
                Description = "Cache de navigation Edge",
                Chemins = { Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache") },
            },
            new()
            {
                Nom = "Cache Google Chrome",
                Description = "Cache de navigation Chrome",
                Chemins = { Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache") },
            },
            new()
            {
                Nom = "Corbeille",
                Description = "Vide la corbeille de tous les lecteurs",
                Chemins = { "$RECYCLE.BIN$" }, // marqueur spécial géré à part
            },
        };

        foreach (var cat in categories)
            cat.TailleBytes = cat.Chemins[0] == "$RECYCLE.BIN$" ? GetRecycleBinSize() : GetFolderSize(cat.Chemins[0]);

        return categories;
    }

    private static long GetFolderSize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { /* fichier verrouillé */ }
            }
        }
        catch { /* accès refusé sur certains sous-dossiers */ }
        return total;
    }

    private static long GetRecycleBinSize()
    {
        // SHQueryRecycleBin donne une estimation fiable et rapide.
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        if (SHQueryRecycleBinW(null, ref info) == 0) return info.i64Size;
        return 0;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBinW(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    public record CleanResult(string Categorie, long OctetsLiberes, int FichiersSupprimes, int Erreurs);

    public List<CleanResult> Clean(IEnumerable<CleanupCategory> selected)
    {
        var results = new List<CleanResult>();
        foreach (var cat in selected)
        {
            if (cat.Chemins.Count == 1 && cat.Chemins[0] == "$RECYCLE.BIN$")
            {
                long before = GetRecycleBinSize();
                SHEmptyRecycleBinW(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                results.Add(new CleanResult(cat.Nom, before, -1, 0));
                continue;
            }

            long freed = 0; int deleted = 0, errors = 0;
            var path = cat.Chemins[0];
            if (Directory.Exists(path))
            {
                foreach (var file in SafeEnumerateFiles(path))
                {
                    try
                    {
                        var len = new FileInfo(file).Length;
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        freed += len;
                        deleted++;
                    }
                    catch { errors++; }
                }
                foreach (var dir in SafeEnumerateDirs(path))
                {
                    try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); }
                    catch { /* pas grave si un sous-dossier ne peut pas être supprimé */ }
                }
            }
            results.Add(new CleanResult(cat.Nom, freed, deleted, errors));
        }
        return results;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList(); }
        catch { return Enumerable.Empty<string>(); }
    }

    private static IEnumerable<string> SafeEnumerateDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList(); }
        catch { return Enumerable.Empty<string>(); }
    }
}
