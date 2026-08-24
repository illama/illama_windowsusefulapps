using System.Management;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

public record ResourceServiceInfo(string Nom, string NomAffichage, double CpuSeconds, double MemoireMb, int Pid);

public record RecommendedService(string Nom, string Affichage, string Raison);

/// <summary>Interroge et pilote les services Windows via WMI (Win32_Service).</summary>
public class ServiceManagerService
{
    public static readonly RecommendedService[] Recommandes =
    {
        new("dmwappushservice", "Service de routage de messages push WAP", "Télémétrie et collecte de données"),
        new("DiagTrack", "Expériences des utilisateurs connectés et télémétrie", "Collecte de données d'utilisation"),
        new("RetailDemo", "Service de démonstration du magasin de détail", "Inutile sauf en magasin"),
        new("XblAuthManager", "Gestionnaire d'authentification Xbox Live", "Inutile sans Xbox"),
        new("XblGameSave", "Sauvegarde de jeux Xbox Live", "Inutile sans Xbox"),
        new("XboxNetApiSvc", "Service réseau Xbox Live", "Inutile sans Xbox"),
        new("XboxGipSvc", "Gestion des contrôleurs Xbox", "Inutile sans manette Xbox"),
        new("WSearch", "Recherche Windows", "Améliore les perfs mais désactive la recherche rapide"),
        new("SysMain", "SysMain (Superfetch)", "Peut ralentir les SSD"),
        new("WbioSrvc", "Service biométrique Windows", "Inutile sans capteur biométrique"),
    };

    public List<ServiceInfo> GetAll(string filter = "All")
    {
        var result = new List<ServiceInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DisplayName, State, StartMode, Description FROM Win32_Service");

        foreach (ManagementObject mo in searcher.Get())
        {
            var state = mo["State"]?.ToString() ?? "";
            var status = state == "Running" ? "Running" : "Stopped";
            if (filter == "Running" && status != "Running") continue;
            if (filter == "Stopped" && status != "Stopped") continue;

            var startMode = mo["StartMode"]?.ToString() switch
            {
                "Auto" => "Automatique",
                "Manual" => "Manuel",
                "Disabled" => "Désactivé",
                var other => other ?? "Inconnu"
            };

            result.Add(new ServiceInfo
            {
                Nom = mo["Name"]?.ToString() ?? "",
                NomAffichage = mo["DisplayName"]?.ToString() ?? "",
                Statut = status,
                TypeDemarrage = startMode,
                Description = mo["Description"]?.ToString() ?? "N/A",
            });
        }

        return result.OrderBy(s => s.NomAffichage, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ManagementObject? Find(string name)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{Escape(name)}'");
        return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
    }

    private static string Escape(string s) => s.Replace("'", "''");

    public (bool Ok, string Message) Start(string name)
    {
        using var svc = Find(name);
        if (svc == null) return (false, "Service introuvable.");
        var result = (uint)svc.InvokeMethod("StartService", null);
        return result == 0 ? (true, "Service démarré.") : (false, $"Échec (code {result}).");
    }

    public (bool Ok, string Message) Stop(string name)
    {
        using var svc = Find(name);
        if (svc == null) return (false, "Service introuvable.");
        var result = (uint)svc.InvokeMethod("StopService", null);
        return result == 0 ? (true, "Service arrêté.") : (false, $"Échec (code {result}).");
    }

    public (bool Ok, string Message) Restart(string name)
    {
        var stop = Stop(name);
        Thread.Sleep(400);
        var start = Start(name);
        return start.Ok ? (true, "Service redémarré.") : start;
    }

    public (bool Ok, string Message) SetStartMode(string name, string mode)
    {
        using var svc = Find(name);
        if (svc == null) return (false, "Service introuvable.");
        var result = (uint)svc.InvokeMethod("ChangeStartMode", new object[] { mode });
        return result == 0 ? (true, "Type de démarrage modifié.") : (false, $"Échec (code {result}).");
    }

    public List<ResourceServiceInfo> GetTopResourceServices(int top = 15)
    {
        var list = new List<ResourceServiceInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DisplayName, ProcessId FROM Win32_Service WHERE ProcessId != 0 AND State = 'Running'");

        var byPid = new Dictionary<int, (string Name, string Display)>();
        foreach (ManagementObject mo in searcher.Get())
        {
            var pid = Convert.ToInt32(mo["ProcessId"]);
            byPid[pid] = (mo["Name"]?.ToString() ?? "", mo["DisplayName"]?.ToString() ?? "");
        }

        foreach (var proc in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (!byPid.TryGetValue(proc.Id, out var info)) continue;
                list.Add(new ResourceServiceInfo(
                    info.Name, info.Display,
                    Math.Round(proc.TotalProcessorTime.TotalSeconds, 2),
                    Math.Round(proc.WorkingSet64 / 1024.0 / 1024, 2),
                    proc.Id));
            }
            catch { /* accès refusé à certains processus système */ }
        }

        return list.OrderByDescending(s => s.CpuSeconds).Take(top).ToList();
    }
}
