namespace SystemManagerPro.Services;

public record EnvVarRow(string Name, string Value);

/// <summary>Nouvelle fonctionnalité : consultation et édition des variables d'environnement
/// utilisateur et système.</summary>
public class EnvironmentVariableService
{
    public List<EnvVarRow> GetAll(EnvironmentVariableTarget scope)
    {
        var vars = Environment.GetEnvironmentVariables(scope);
        var list = new List<EnvVarRow>();
        foreach (System.Collections.DictionaryEntry entry in vars)
            list.Add(new EnvVarRow(entry.Key.ToString() ?? "", entry.Value?.ToString() ?? ""));
        return list.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Set(string name, string value, EnvironmentVariableTarget scope) =>
        Environment.SetEnvironmentVariable(name, value, scope);

    public void Delete(string name, EnvironmentVariableTarget scope) =>
        Environment.SetEnvironmentVariable(name, null, scope);
}
