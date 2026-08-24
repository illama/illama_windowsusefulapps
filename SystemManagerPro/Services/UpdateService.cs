using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemManagerPro.Services;

public record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string DownloadUrl, string ReleaseUrl, string Notes);

/// <summary>Nouvelle fonctionnalité : vérifie les nouvelles versions publiées sur le dépôt GitHub
/// du projet (Releases) et permet de télécharger + lancer l'installateur en un clic.</summary>
public class UpdateService
{
    private const string Owner = "illama";
    private const string Repo = "illama_windowsusefulapps";
    private const string AssetNameHint = "Setup"; // on cherche l'asset .exe dont le nom contient ce mot

    private static readonly HttpClient Http = BuildClient();

    private static HttpClient BuildClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GestionnaireSystemePro-UpdateChecker");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    private class GithubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("body")] public string Body { get; set; } = "";
        [JsonPropertyName("assets")] public List<GithubAsset> Assets { get; set; } = new();
    }

    private class GithubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }

    public static string CurrentVersionString()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
    }

    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        string current = CurrentVersionString();
        var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

        GithubRelease? release;
        try
        {
            var json = await Http.GetStringAsync(url);
            release = JsonSerializer.Deserialize<GithubRelease>(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Impossible de contacter GitHub : " + ex.Message);
        }

        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            return new UpdateInfo(false, current, current, "", "", "Aucune release trouvée.");

        string latestTag = release.TagName.TrimStart('v', 'V');
        bool isNewer = CompareVersions(latestTag, current) > 0;

        var asset = release.Assets.FirstOrDefault(a => a.Name.Contains(AssetNameHint, StringComparison.OrdinalIgnoreCase)
                                                        && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        return new UpdateInfo(isNewer, current, latestTag, asset?.DownloadUrl ?? "", release.HtmlUrl, release.Body);
    }

    private static int CompareVersions(string a, string b)
    {
        Version.TryParse(a, out var va);
        Version.TryParse(b, out var vb);
        va ??= new Version(0, 0, 0);
        vb ??= new Version(0, 0, 0);
        return va.CompareTo(vb);
    }

    /// <summary>Télécharge l'installateur vers un fichier temporaire et renvoie son chemin.</summary>
    public async Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<double>? progress = null)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "GestionnaireSystemeProSetup.exe");

        using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using var httpStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await httpStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            readTotal += read;
            if (totalBytes is > 0)
                progress?.Report((double)readTotal / totalBytes.Value * 100);
        }

        return tempPath;
    }
}
