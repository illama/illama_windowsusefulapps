using System.IO;
using System.Management;
using System.Security.Principal;

namespace SystemManagerPro.Services;

public record SystemSnapshot(
    double CpuPercent,
    double RamPercent,
    double RamUsedGb,
    double RamTotalGb,
    List<DiskSnapshot> Disks,
    TimeSpan Uptime,
    string ComputerName,
    string UserName,
    string OsCaption,
    bool IsAdmin);

public record DiskSnapshot(string Label, double UsedGb, double TotalGb, double Percent);

/// <summary>Récupère les informations système (CPU, RAM, disques, uptime...) affichées sur le tableau de bord.</summary>
public class SystemInfoService
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public SystemSnapshot GetSnapshot()
    {
        double cpu = 0;
        double ramPercent = 0, ramUsedGb = 0, ramTotalGb = 0;
        TimeSpan uptime = TimeSpan.Zero;
        string osCaption = Environment.OSVersion.VersionString;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var loads = searcher.Get().Cast<ManagementObject>()
                .Select(mo => Convert.ToDouble(mo["LoadPercentage"] ?? 0d))
                .ToList();
            if (loads.Count > 0) cpu = loads.Average();
        }
        catch { /* ignore, laisse 0 */ }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT FreePhysicalMemory, TotalVisibleMemorySize, LastBootUpTime, Caption FROM Win32_OperatingSystem");
            var os = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (os != null)
            {
                double freeKb = Convert.ToDouble(os["FreePhysicalMemory"]);
                double totalKb = Convert.ToDouble(os["TotalVisibleMemorySize"]);
                ramTotalGb = totalKb / 1024 / 1024;
                ramUsedGb = (totalKb - freeKb) / 1024 / 1024;
                ramPercent = totalKb > 0 ? (totalKb - freeKb) / totalKb * 100 : 0;

                var bootStr = os["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(bootStr))
                {
                    var boot = ManagementDateTimeConverter.ToDateTime(bootStr);
                    uptime = DateTime.Now - boot;
                }
                osCaption = os["Caption"]?.ToString()?.Trim() ?? osCaption;
            }
        }
        catch { /* ignore */ }

        var disks = new List<DiskSnapshot>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            double totalGb = drive.TotalSize / 1024.0 / 1024 / 1024;
            double freeGb = drive.TotalFreeSpace / 1024.0 / 1024 / 1024;
            double usedGb = totalGb - freeGb;
            double pct = totalGb > 0 ? usedGb / totalGb * 100 : 0;
            disks.Add(new DiskSnapshot(drive.Name.TrimEnd('\\'), usedGb, totalGb, pct));
        }

        return new SystemSnapshot(
            Math.Round(cpu, 1),
            Math.Round(ramPercent, 1),
            Math.Round(ramUsedGb, 1),
            Math.Round(ramTotalGb, 1),
            disks,
            uptime,
            Environment.MachineName,
            Environment.UserName,
            osCaption,
            IsAdministrator());
    }
}
