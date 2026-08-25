using System.Text.RegularExpressions;

namespace SystemManagerPro.Services;

public record PowerPlanInfo(string Guid, string Name, bool Active);

/// <summary>Nouvelle fonctionnalité : bascule entre les modes d'alimentation Windows via powercfg.</summary>
public class PowerPlanService
{
    private static readonly Regex PlanRegex = new(
        @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*\((.+?)\)(\s*\*)?",
        RegexOptions.Compiled);

    public List<PowerPlanInfo> GetPlans()
    {
        var result = ProcessRunner.Run("powercfg", "/list");
        var plans = new List<PowerPlanInfo>();
        foreach (var line in result.StdOut.Split('\n'))
        {
            var m = PlanRegex.Match(line);
            if (m.Success)
                plans.Add(new PowerPlanInfo(m.Groups[1].Value, m.Groups[2].Value.Trim(), m.Groups[3].Success));
        }
        return plans;
    }

    public (bool Ok, string Message) Activate(string guid)
    {
        var result = ProcessRunner.Run("powercfg", $"/setactive {guid}");
        return result.ExitCode == 0 ? (true, "Mode d'alimentation activé.") : (false, "Échec : " + result.StdErr);
    }
}
