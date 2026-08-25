using System.IO;

namespace SystemManagerPro.Services;

public record HostsEntry(int LineIndex, string Ip, string Hostname, bool Enabled);

/// <summary>Nouvelle fonctionnalité : édition du fichier hosts (blocage de domaines, redirections locales).
/// Travaille sur une copie en mémoire des lignes ; rien n'est écrit sur disque avant "Enregistrer".</summary>
public class HostsFileService
{
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public List<string> ReadLines() =>
        File.Exists(HostsPath) ? File.ReadAllLines(HostsPath).ToList() : new List<string>();

    public void WriteLines(List<string> lines)
    {
        BackupOnce();
        File.WriteAllLines(HostsPath, lines);
    }

    private static void BackupOnce()
    {
        var backup = HostsPath + ".backup";
        if (File.Exists(HostsPath) && !File.Exists(backup))
            File.Copy(HostsPath, backup);
    }

    public List<HostsEntry> ParseEntries(List<string> lines)
    {
        var entries = new List<HostsEntry>();
        for (int i = 0; i < lines.Count; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            bool enabled = !raw.StartsWith('#');
            var content = enabled ? raw : raw.TrimStart('#').TrimStart();
            var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!System.Net.IPAddress.TryParse(parts[0], out _)) continue; // ignore les vrais commentaires

            entries.Add(new HostsEntry(i, parts[0], parts[1], enabled));
        }
        return entries;
    }

    public void ToggleEntry(List<string> lines, int lineIndex)
    {
        var line = lines[lineIndex];
        var trimmed = line.TrimStart();
        lines[lineIndex] = trimmed.StartsWith('#') ? trimmed.TrimStart('#').TrimStart() : "# " + trimmed;
    }

    public void RemoveEntry(List<string> lines, int lineIndex) => lines.RemoveAt(lineIndex);

    public void AddEntry(List<string> lines, string ip, string hostname) =>
        lines.Add($"{ip}\t{hostname}");
}
