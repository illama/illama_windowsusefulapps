using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly UpdateService _updates = new();
    private UpdateInfo? _lastCheck;

    public SettingsView()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        StartWithWindowsToggle.IsChecked = _settings.Current.StartWithWindows;
        StartMinimizedToggle.IsChecked = _settings.Current.StartMinimized;
        CloseToTrayToggle.IsChecked = _settings.Current.CloseToTray;
        CheckUpdatesOnStartupToggle.IsChecked = _settings.Current.CheckUpdatesOnStartup;
        BuildAbout();
    }

    private void BuildAbout()
    {
        AboutStack.Children.Clear();
        AddRow("Version", UpdateService.CurrentVersionString());
        AddRow("Emplacement", Environment.ProcessPath ?? "—");
        AddRow("Dépôt", "github.com/illama/illama_windowsusefulapps");
    }

    private void CheckUpdatesOnStartupToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.SetCheckUpdatesOnStartup(CheckUpdatesOnStartupToggle.IsChecked == true);
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateStatusText.Text = "Vérification en cours…";
        InstallUpdateBtn.Visibility = Visibility.Collapsed;
        try
        {
            _lastCheck = await _updates.CheckForUpdateAsync();
            if (_lastCheck.Available)
            {
                UpdateStatusText.Text = $"Une nouvelle version est disponible : v{_lastCheck.LatestVersion} " +
                                         $"(version actuelle : v{_lastCheck.CurrentVersion}).";
                InstallUpdateBtn.Visibility = string.IsNullOrEmpty(_lastCheck.DownloadUrl) ? Visibility.Collapsed : Visibility.Visible;
                LogService.Instance.Log($"Mise à jour disponible : v{_lastCheck.LatestVersion}.", LogLevel.Info);
            }
            else
            {
                UpdateStatusText.Text = $"Vous utilisez déjà la dernière version (v{_lastCheck.CurrentVersion}).";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Échec de la vérification : " + ex.Message;
            LogService.Instance.Log("Échec de la vérification des mises à jour : " + ex.Message, LogLevel.Error);
        }
        finally { CheckUpdateBtn.IsEnabled = true; }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCheck is not { Available: true } info || string.IsNullOrEmpty(info.DownloadUrl)) return;

        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Installer la mise à jour",
            $"Télécharger et installer la version v{info.LatestVersion} ?\nL'application se fermera pendant l'installation.",
            "Télécharger"))
            return;

        InstallUpdateBtn.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        var progress = new Progress<double>(p => DownloadProgress.Value = p);

        try
        {
            string path = await _updates.DownloadInstallerAsync(info.DownloadUrl, progress);
            LogService.Instance.Log("Mise à jour téléchargée, lancement de l'installateur…", LogLevel.Success);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            (owner as MainWindow)?.ExitApplication();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Échec du téléchargement : " + ex.Message;
            LogService.Instance.Log("Échec du téléchargement de la mise à jour : " + ex.Message, LogLevel.Error);
            InstallUpdateBtn.IsEnabled = true;
        }
    }

    private void AddRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted") });
        var val = new TextBlock { Text = value, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 420 };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        AboutStack.Children.Add(grid);
    }

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = StartWithWindowsToggle.IsChecked == true;
        try
        {
            _settings.SetStartWithWindows(enabled);
            LogService.Instance.Log(
                enabled ? "Démarrage avec Windows activé." : "Démarrage avec Windows désactivé.", LogLevel.Success);
        }
        catch (Exception ex)
        {
            StartWithWindowsToggle.IsChecked = !enabled;
            LogService.Instance.Log("Échec de la modification : " + ex.Message, LogLevel.Error);
        }
    }

    private void StartMinimizedToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = StartMinimizedToggle.IsChecked == true;
        _settings.SetStartMinimized(enabled);
        LogService.Instance.Log(
            enabled ? "L'application démarrera réduite." : "L'application démarrera en fenêtre normale.", LogLevel.Success);
    }

    private void CloseToTrayToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = CloseToTrayToggle.IsChecked == true;
        _settings.SetCloseToTray(enabled);
        LogService.Instance.Log(
            enabled ? "La fermeture réduira désormais l'application dans la barre d'état système."
                    : "La fermeture quittera désormais l'application normalement.", LogLevel.Success);
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Quitter l'application", "Fermer complètement Gestionnaire Système Pro ?", "Quitter", danger: true))
            return;

        (owner as MainWindow)?.ExitApplication();
    }
}
