using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : liste les programmes installés (registre Uninstall,
/// 32/64 bits + par utilisateur) et permet de lancer leur désinstallateur.</summary>
public class UninstallerService
{
    private static readonly (RegistryKey Hive, string Path)[] Roots =
    {
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public List<InstalledProgram> GetInstalled()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<InstalledProgram>();

        foreach (var (hive, path) in Roots)
        {
            using var root = hive.OpenSubKey(path);
            if (root == null) continue;

            foreach (var sub in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(sub);
                if (key == null) continue;

                var name = key.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1) continue;
                if (key.GetValue("ParentKeyName") != null) continue; // mise à jour, pas une appli
                if (!seen.Add(name)) continue;

                var uninstall = key.GetValue("QuietUninstallString") as string
                                 ?? key.GetValue("UninstallString") as string ?? "";
                bool quiet = key.GetValue("QuietUninstallString") != null;

                long sizeKb = 0;
                try { sizeKb = Convert.ToInt64(key.GetValue("EstimatedSize", 0)); } catch { /* absent */ }

                var installDate = key.GetValue("InstallDate") as string ?? "";
                if (installDate.Length == 8)
                    installDate = $"{installDate[6..8]}/{installDate[4..6]}/{installDate[..4]}";

                result.Add(new InstalledProgram
                {
                    Nom = name,
                    Version = key.GetValue("DisplayVersion") as string ?? "—",
                    Editeur = key.GetValue("Publisher") as string ?? "—",
                    DateInstall = installDate,
                    TailleMB = Math.Round(sizeKb / 1024.0, 1),
                    UninstallString = uninstall,
                    Silencieux = quiet,
                });
            }
        }

        return result.OrderBy(p => p.Nom, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Lance le désinstallateur du programme (interactif — l'utilisateur confirme dans sa propre UI).</summary>
    public void Uninstall(InstalledProgram program)
    {
        if (string.IsNullOrWhiteSpace(program.UninstallString))
            throw new InvalidOperationException("Aucune commande de désinstallation connue pour ce programme.");

        string cmd = program.UninstallString;
        string fileName, arguments;

        if (cmd.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase) || cmd.Contains(".msi", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "msiexec.exe";
            var idx = cmd.IndexOf('/');
            arguments = idx >= 0 ? cmd[idx..] : "";
        }
        else if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            fileName = cmd[1..end];
            arguments = cmd[(end + 1)..].Trim();
        }
        else
        {
            var parts = cmd.Split(' ', 2);
            fileName = parts[0];
            arguments = parts.Length > 1 ? parts[1] : "";
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
        });
    }
}
