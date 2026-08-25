namespace SystemManagerPro.Services;

public record FirewallProfileState(string Profile, bool Enabled);

/// <summary>Nouvelle fonctionnalité : active/désactive le Pare-feu Windows par profil (Domaine, Privé, Public).</summary>
public class FirewallService
{
    private static readonly (string Key, string Label)[] Profiles =
    {
        ("domainprofile", "Domaine"),
        ("privateprofile", "Privé"),
        ("publicprofile", "Public"),
    };

    public List<FirewallProfileState> GetStates()
    {
        var result = ProcessRunner.Run("netsh", "advfirewall show allprofiles state");
        var states = new List<FirewallProfileState>();
        foreach (var (key, label) in Profiles)
        {
            // Le texte de sortie de netsh contient des blocs "Domain Profile Settings:" / "State  ON|OFF"
            // (localisés). On repère le bloc par mot-clé anglais ET français pour rester robuste.
            bool? on = ParseBlockState(result.StdOut, key);
            states.Add(new FirewallProfileState(label, on ?? true));
        }
        return states;
    }

    private static bool? ParseBlockState(string output, string profileKey)
    {
        var lines = output.Split('\n');
        bool inBlock = false;
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains(profileKey)) { inBlock = true; continue; }
            if (inBlock && (lower.Contains("profile") && lower.Contains("settings"))) break;
            if (inBlock && lower.TrimStart().StartsWith("state"))
            {
                if (lower.Contains("off") || lower.Contains("désactivé")) return false;
                if (lower.Contains("on") || lower.Contains("activé")) return true;
            }
        }
        return null;
    }

    public (bool Ok, string Message) SetProfile(string profileLabel, bool enabled)
    {
        var key = profileLabel switch
        {
            "Domaine" => "domainprofile",
            "Privé" => "privateprofile",
            "Public" => "publicprofile",
            _ => "allprofiles",
        };
        var result = ProcessRunner.Run("netsh", $"advfirewall set {key} state {(enabled ? "on" : "off")}");
        return result.ExitCode == 0
            ? (true, $"Pare-feu ({profileLabel}) {(enabled ? "activé" : "désactivé")}.")
            : (false, "Échec : " + result.StdErr);
    }
}
