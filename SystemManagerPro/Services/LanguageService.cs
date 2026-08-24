using System.Text.Json;
using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Gère les langues d'affichage/saisie de Windows. S'appuie sur les cmdlets PowerShell
/// Get-/Set-WinUserLanguageList (module International) qui n'ont pas d'équivalent .NET direct.</summary>
public class LanguageService
{
    private record LangDto(string Tag, string Name);

    public List<LanguageEntry> GetInstalled()
    {
        var result = ProcessRunner.RunPowerShell(
            "Get-WinUserLanguageList | ForEach-Object { [PSCustomObject]@{ Tag = $_.LanguageTag; Name = $_.EnglishName } } | ConvertTo-Json -Compress");

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
            throw new InvalidOperationException("Impossible de lire les langues installées : " + result.StdErr);

        var json = result.StdOut.Trim();
        var items = new List<LangDto>();
        try
        {
            if (json.StartsWith('['))
                items = JsonSerializer.Deserialize<List<LangDto>>(json) ?? new();
            else
            {
                var single = JsonSerializer.Deserialize<LangDto>(json);
                if (single != null) items.Add(single);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Réponse inattendue de PowerShell : " + ex.Message);
        }

        return items.Select(i => new LanguageEntry { Tag = i.Tag, DisplayName = i.Name }).ToList();
    }

    /// <summary>Ne conserve que les tags fournis, supprime toutes les autres langues installées.</summary>
    public void ApplyKeepOnly(IEnumerable<string> tagsToKeep)
    {
        var tags = tagsToKeep.Select(t => $"'{t}'");
        var list = string.Join(",", tags);
        var script = $"Set-WinUserLanguageList -LanguageList @({list}) -Force";
        var result = ProcessRunner.RunPowerShell(script, timeoutMs: 60000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Échec de la mise à jour des langues : " + result.StdErr);
    }

    /// <summary>Bloque l'ajout automatique de langues, corrige ctfmon et désactive la synchro (comme le script d'origine).</summary>
    public void ApplyHardeningTweaks()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Control Panel\International");
            key.SetValue("BlockUserInputMethodsForSignIn", 1, RegistryValueKind.DWord);
            key.SetValue("RestrictLanguagePacksAndFeaturesInstall", 1, RegistryValueKind.DWord);
        }

        using (var run = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
            run.SetValue("ctfmon.exe", @"C:\Windows\System32\ctfmon.exe", RegistryValueKind.String);

        using (var sync = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Language"))
            sync.SetValue("Enabled", 0, RegistryValueKind.DWord);
    }
}
