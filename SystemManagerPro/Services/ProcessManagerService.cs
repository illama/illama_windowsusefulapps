using System.Diagnostics;

namespace SystemManagerPro.Services;

public record ProcessRow(int Pid, string Name, double CpuPercent, double MemoryMb, int Threads, DateTime? StartTime);

/// <summary>Nouvelle fonctionnalité : mini gestionnaire des tâches (liste des processus, usage CPU/mémoire,
/// possibilité de forcer l'arrêt). Le CPU% est calculé entre deux appels successifs à GetSnapshot.</summary>
public class ProcessManagerService
{
    private Dictionary<int, TimeSpan> _lastCpuTimes = new();
    private DateTime _lastSampleTime = DateTime.MinValue;

    public List<ProcessRow> GetSnapshot()
    {
        var now = DateTime.Now;
        var elapsedMs = _lastSampleTime == DateTime.MinValue ? 0 : (now - _lastSampleTime).TotalMilliseconds;
        int cpuCount = Math.Max(1, Environment.ProcessorCount);

        var rows = new List<ProcessRow>();
        var currentCpuTimes = new Dictionary<int, TimeSpan>();

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var cpuTime = proc.TotalProcessorTime;
                currentCpuTimes[proc.Id] = cpuTime;

                double cpuPercent = 0;
                if (elapsedMs > 0 && _lastCpuTimes.TryGetValue(proc.Id, out var prev))
                {
                    var deltaMs = (cpuTime - prev).TotalMilliseconds;
                    cpuPercent = Math.Max(0, deltaMs / (elapsedMs * cpuCount) * 100);
                }

                rows.Add(new ProcessRow(
                    proc.Id, proc.ProcessName,
                    Math.Round(cpuPercent, 1),
                    Math.Round(proc.WorkingSet64 / 1024.0 / 1024, 1),
                    proc.Threads.Count,
                    TryGetStartTime(proc)));
            }
            catch { /* accès refusé à certains processus système, ou processus déjà terminé */ }
            finally { proc.Dispose(); }
        }

        _lastCpuTimes = currentCpuTimes;
        _lastSampleTime = now;
        return rows.OrderByDescending(r => r.CpuPercent).ThenByDescending(r => r.MemoryMb).ToList();
    }

    private static DateTime? TryGetStartTime(Process p)
    {
        try { return p.StartTime; } catch { return null; }
    }

    public (bool Ok, string Message) EndTask(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            string name = proc.ProcessName;
            proc.Kill(entireProcessTree: true);
            return (true, $"Processus « {name} » (PID {pid}) arrêté.");
        }
        catch (Exception ex)
        {
            return (false, "Impossible d'arrêter ce processus : " + ex.Message);
        }
    }
}
