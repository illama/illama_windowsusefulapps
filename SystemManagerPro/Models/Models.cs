namespace SystemManagerPro.Models;

public enum StartupSourceType { Registry, ScheduledTask, StartupFolder }

public class StartupApp : ObservableObject
{
    public string Nom { get; set; } = "";
    public string Chemin { get; set; } = "";
    public StartupSourceType Type { get; set; }
    public string TypeLabel => Type switch
    {
        StartupSourceType.Registry => "Registre",
        StartupSourceType.ScheduledTask => "Tâche planifiée",
        StartupSourceType.StartupFolder => "Dossier Démarrage",
        _ => "Inconnu"
    };
    public string Emplacement { get; set; } = "";
    private bool _actif;
    public bool Actif { get => _actif; set => Set(ref _actif, value); }
    public string StatutLabel => Actif ? "Actif" : "Désactivé";
}

public class ServiceInfo : ObservableObject
{
    public string Nom { get; set; } = "";
    public string NomAffichage { get; set; } = "";
    private string _statut = "";
    public string Statut { get => _statut; set => Set(ref _statut, value); }
    private string _typeDemarrage = "";
    public string TypeDemarrage { get => _typeDemarrage; set => Set(ref _typeDemarrage, value); }
    public string Description { get; set; } = "";
    public bool EnCours => Statut == "Running" || Statut == "En cours";
}

public class LanguageEntry : ObservableObject
{
    public string Tag { get; set; } = "";
    public string DisplayName { get; set; } = "";
    private bool _keep;
    public bool Keep { get => _keep; set => Set(ref _keep, value); }
}

public record KeyOption(string Name, ushort Code);

public class KeyMapping
{
    public ushort SourceCode { get; set; }
    public ushort DestCode { get; set; }
    public string SourceName { get; set; } = "";
    public string DestName { get; set; } = "";
}

public class InstalledProgram
{
    public string Nom { get; set; } = "";
    public string Version { get; set; } = "";
    public string Editeur { get; set; } = "";
    public string DateInstall { get; set; } = "";
    public double TailleMB { get; set; }
    public string UninstallString { get; set; } = "";
    public bool Silencieux { get; set; }
}

public class CleanupCategory : ObservableObject
{
    public string Nom { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Chemins { get; set; } = new();
    private bool _checked = true;
    public bool IsChecked { get => _checked; set => Set(ref _checked, value); }
    private long _taille;
    public long TailleBytes { get => _taille; set { Set(ref _taille, value); Raise(nameof(TailleLabel)); } }
    public string TailleLabel => FormatBytes(TailleBytes);
    public static string FormatBytes(long bytes)
    {
        string[] units = { "o", "Ko", "Mo", "Go" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {units[i]}";
    }
}

public enum LogLevel { Info, Success, Warning, Error }

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string TimeLabel => Timestamp.ToString("HH:mm:ss");
}

public class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool CheckUpdatesOnStartup { get; set; } = true;
}

public class QuickTweak : ObservableObject
{
    public string Nom { get; set; } = "";
    public string Description { get; set; } = "";
    private bool _isOn;
    public bool IsOn { get => _isOn; set => Set(ref _isOn, value); }
    public Func<bool>? Getter { get; set; }
    public Action<bool>? Setter { get; set; }
}
