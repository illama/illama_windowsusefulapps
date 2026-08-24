using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SystemManagerPro.Models;
using SystemManagerPro.Services;
using SystemManagerPro.Views;

namespace SystemManagerPro;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, UserControl> _views = new();
    private DispatcherTimerWrapper? _toastTimer;
    private readonly TrayIconService _tray;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();

        UserLabel.Text = $"{Environment.UserName} · {Environment.MachineName}";
        if (!SystemInfoService.IsAdministrator())
        {
            MessageBox.Show(
                "Cette application doit être lancée en tant qu'administrateur pour fonctionner correctement.\n" +
                "Certaines fonctionnalités (services, registre, langues) échoueront sans ces droits.",
                "Droits insuffisants", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        LogService.Instance.Entries.CollectionChanged += Entries_CollectionChanged;
        LogService.Instance.Log("Application démarrée.", LogLevel.Info);

        _tray = new TrayIconService("Gestionnaire Système Pro");
        _tray.OpenRequested += () => Dispatcher.Invoke(RestoreFromTray);
        _tray.ExitRequested += () => Dispatcher.Invoke(ExitApplication);

        if (SettingsService.Instance.Current.StartMinimized)
            WindowState = WindowState.Minimized;

        // Coché après InitializeComponent() (pas en XAML) : sinon l'événement Checked se déclenche
        // pendant le parsing du XAML, avant que MainContent (déclaré plus bas) ne soit câblé.
        NavDashboard.IsChecked = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose && SettingsService.Instance.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.Show();
            return;
        }
        _tray.Dispose();
        base.OnClosing(e);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _tray.Hide();
    }

    /// <summary>Quitte réellement l'application (depuis l'icône de la zone de notification ou les Paramètres),
    /// en contournant la redirection vers la barre d'état système.</summary>
    public void ExitApplication()
    {
        _forceClose = true;
        Close();
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null) return;
        var entry = (LogEntry)e.NewItems[0]!;
        if (entry.Level == LogLevel.Info) return; // pas de toast pour le bruit informatif
        ShowToast(entry);
    }

    private void ShowToast(LogEntry entry)
    {
        ToastText.Text = entry.Message;
        ToastDot.Fill = entry.Level switch
        {
            LogLevel.Success => (Brush)FindResource("Success"),
            LogLevel.Warning => (Brush)FindResource("Warning"),
            LogLevel.Error => (Brush)FindResource("Danger"),
            _ => (Brush)FindResource("Accent"),
        };
        Toast.Visibility = Visibility.Visible;
        Toast.Opacity = 0;
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimerWrapper(TimeSpan.FromSeconds(4), () =>
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
            Toast.BeginAnimation(OpacityProperty, fade);
        });
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        var key = rb.Name switch
        {
            nameof(NavDashboard) => "dashboard",
            nameof(NavStartup) => "startup",
            nameof(NavServices) => "services",
            nameof(NavLanguage) => "language",
            nameof(NavKeyboard) => "keyboard",
            nameof(NavCleanup) => "cleanup",
            nameof(NavNetwork) => "network",
            nameof(NavUninstaller) => "uninstaller",
            nameof(NavTweaks) => "tweaks",
            nameof(NavLogs) => "logs",
            nameof(NavSettings) => "settings",
            _ => "dashboard"
        };
        NavigateTo(key);
    }

    private void NavigateTo(string key)
    {
        if (!_views.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "dashboard" => new DashboardView(),
                "startup" => new StartupView(),
                "services" => new ServicesView(),
                "language" => new LanguageView(),
                "keyboard" => new KeyboardView(),
                "cleanup" => new CleanupView(),
                "network" => new NetworkView(),
                "uninstaller" => new UninstallerView(),
                "tweaks" => new TweaksView(),
                "logs" => new LogsView(),
                "settings" => new SettingsView(),
                _ => new DashboardView(),
            };
            _views[key] = view;
        }
        MainContent.Content = view;
        if (view is IActivatable activatable) activatable.OnActivated();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { MaximizeRestore_Click(sender, e); return; }
        try { DragMove(); } catch { /* ignoré si le bouton a déjà été relâché */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaxIcon.Text = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Petit minuteur jetable pour piloter la disparition du toast sans dépendances externes.</summary>
internal sealed class DispatcherTimerWrapper
{
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    public DispatcherTimerWrapper(TimeSpan delay, Action callback)
    {
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        _timer.Tick += (_, _) => { Stop(); callback(); };
        _timer.Start();
    }
    public void Stop() => _timer.Stop();
}

/// <summary>Permet à une vue de recharger ses données lorsqu'elle redevient visible.</summary>
public interface IActivatable
{
    void OnActivated();
}
