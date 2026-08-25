using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private record CommandItem(string Icon, string Title, string Subtitle, Action Execute);
    private List<CommandItem> _commands = new();
    private List<CommandItem> _paletteFiltered = new();
    private int _paletteIndex;

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

        BuildCommands();
        _ = CheckUpdatesInBackgroundAsync();

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

    private async Task CheckUpdatesInBackgroundAsync()
    {
        if (!SettingsService.Instance.Current.CheckUpdatesOnStartup) return;
        try
        {
            var info = await new UpdateService().CheckForUpdateAsync();
            if (info.Available)
            {
                SettingsUpdateDot.Visibility = Visibility.Visible;
                var pulse = new DoubleAnimation(1, 1.7, TimeSpan.FromMilliseconds(700))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase(),
                };
                UpdateDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                UpdateDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
                LogService.Instance.Log($"Mise à jour disponible : v{info.LatestVersion} (voir Paramètres).", LogLevel.Info);
            }
        }
        catch { /* vérification en arrière-plan : échec silencieux (pas de connexion, etc.) */ }
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
            nameof(NavProcesses) => "processes",
            nameof(NavLanguage) => "language",
            nameof(NavKeyboard) => "keyboard",
            nameof(NavInstaller) => "installer",
            nameof(NavCleanup) => "cleanup",
            nameof(NavNetwork) => "network",
            nameof(NavUninstaller) => "uninstaller",
            nameof(NavAdvanced) => "advanced",
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
                "processes" => new ProcessesView(),
                "language" => new LanguageView(),
                "keyboard" => new KeyboardView(),
                "installer" => new InstallerView(),
                "cleanup" => new CleanupView(),
                "network" => new NetworkView(),
                "uninstaller" => new UninstallerView(),
                "advanced" => new AdvancedView(),
                "tweaks" => new TweaksView(),
                "logs" => new LogsView(),
                "settings" => new SettingsView(),
                _ => new DashboardView(),
            };
            _views[key] = view;
        }
        AnimateContentSwap(view);
    }

    /// <summary>Petit fondu enchaîné + glissement vertical entre deux pages, pour une transition moins sèche.</summary>
    private void AnimateContentSwap(UserControl view)
    {
        var fadeOut = new DoubleAnimation(MainContent.Opacity, 0, TimeSpan.FromMilliseconds(90));
        fadeOut.Completed += (_, _) =>
        {
            MainContent.Content = view;
            if (view is IActivatable activatable) activatable.OnActivated();

            ContentTransform.Y = 10;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var slideIn = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            MainContent.BeginAnimation(OpacityProperty, fadeIn);
            ContentTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
        };
        MainContent.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { MaximizeRestore_Click(sender, e); return; }
        try { DragMove(); } catch { /* ignoré si le bouton a déjà été relâché */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaxIcon.Text = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ===================== Palette de commandes (Ctrl+K) =====================

    private void BuildCommands()
    {
        _commands = new List<CommandItem>
        {
            new("🏠", "Tableau de bord", "Vue d'ensemble du système", () => Select(NavDashboard)),
            new("🚀", "Démarrage", "Applications au démarrage de Windows", () => Select(NavStartup)),
            new("🧩", "Services", "Démarrer, arrêter, configurer les services", () => Select(NavServices)),
            new("📊", "Processus", "Gestionnaire des tâches", () => Select(NavProcesses)),
            new("🌐", "Langues", "Langues d'affichage et de saisie", () => Select(NavLanguage)),
            new("⌨", "Clavier", "Remapping de touches", () => Select(NavKeyboard)),
            new("📦", "Installateur", "Navigateurs, launchers de jeux, utilitaires", () => Select(NavInstaller)),
            new("🧹", "Nettoyage", "Fichiers temporaires, cache, corbeille", () => Select(NavCleanup)),
            new("📡", "Réseau", "Adaptateurs, DNS, ping", () => Select(NavNetwork)),
            new("🗑", "Programmes", "Désinstaller des applications", () => Select(NavUninstaller)),
            new("🧬", "Avancé", "Variables d'environnement, Hosts, alimentation", () => Select(NavAdvanced)),
            new("🛠", "Réglages rapides", "Interrupteurs système", () => Select(NavTweaks)),
            new("📜", "Journal", "Historique des actions", () => Select(NavLogs)),
            new("⚙️", "Paramètres", "Démarrage, fermeture, mises à jour", () => Select(NavSettings)),
            new("🧽", "Vider le cache DNS", "Action rapide", QuickFlushDns),
            new("🛡️", "Créer un point de restauration", "Action rapide", QuickRestorePoint),
            new("🚪", "Quitter l'application", "Ferme complètement Gestionnaire Système Pro", ExitApplication),
        };
    }

    private void Select(RadioButton rb)
    {
        rb.IsChecked = true;
        ClosePalette();
    }

    private void QuickFlushDns()
    {
        ClosePalette();
        var result = new NetworkService().FlushDns();
        LogService.Instance.Log(result.ExitCode == 0 ? "Cache DNS vidé avec succès." : "Échec du vidage du cache DNS.",
            result.ExitCode == 0 ? LogLevel.Success : LogLevel.Error);
    }

    private void QuickRestorePoint()
    {
        ClosePalette();
        var (ok, message) = new TweaksService().CreateRestorePoint("Gestionnaire Système Pro - Point manuel");
        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
    }

    private void OpenPalette_Click(object sender, RoutedEventArgs e) => OpenPalette();

    private void OpenPalette()
    {
        PaletteInput.Text = "";
        RenderPaletteResults("");
        PaletteOverlay.Visibility = Visibility.Visible;
        PaletteOverlay.Opacity = 0;
        PaletteOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        Keyboard.Focus(PaletteInput);
    }

    private void ClosePalette()
    {
        if (PaletteOverlay.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
        fade.Completed += (_, _) => PaletteOverlay.Visibility = Visibility.Collapsed;
        PaletteOverlay.BeginAnimation(OpacityProperty, fade);
        Keyboard.ClearFocus();
    }

    private void PaletteOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ClosePalette();
    private void PaletteBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void PaletteInput_TextChanged(object sender, TextChangedEventArgs e) => RenderPaletteResults(PaletteInput.Text);

    private void RenderPaletteResults(string term)
    {
        _paletteFiltered = string.IsNullOrWhiteSpace(term)
            ? _commands
            : _commands.Where(c => c.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                                 || c.Subtitle.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        _paletteIndex = 0;
        RenderPaletteRows();
    }

    private void RenderPaletteRows()
    {
        PaletteResults.Children.Clear();
        if (_paletteFiltered.Count == 0)
        {
            PaletteResults.Children.Add(new TextBlock
            {
                Text = "Aucun résultat.", Style = (Style)FindResource("Muted"), Margin = new Thickness(14),
            });
            return;
        }

        for (int i = 0; i < _paletteFiltered.Count; i++)
        {
            var cmd = _paletteFiltered[i];
            bool selected = i == _paletteIndex;

            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 2),
                Background = selected ? (Brush)FindResource("BgElevated3") : Brushes.Transparent,
                Cursor = Cursors.Hand,
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock { Text = cmd.Icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock { Text = cmd.Title, FontSize = 13, FontWeight = FontWeights.SemiBold });
            textStack.Children.Add(new TextBlock { Text = cmd.Subtitle, FontSize = 11.5, Foreground = (Brush)FindResource("TextSecondary"), Margin = new Thickness(0, 2, 0, 0) });
            stack.Children.Add(textStack);
            row.Child = stack;

            row.MouseLeftButtonUp += (_, _) => cmd.Execute();
            row.MouseEnter += (_, _) => { _paletteIndex = i; RenderPaletteRows(); };

            PaletteResults.Children.Add(row);
        }
    }

    private void PaletteInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (_paletteFiltered.Count > 0) _paletteIndex = (_paletteIndex + 1) % _paletteFiltered.Count;
            RenderPaletteRows();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_paletteFiltered.Count > 0) _paletteIndex = (_paletteIndex - 1 + _paletteFiltered.Count) % _paletteFiltered.Count;
            RenderPaletteRows();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_paletteFiltered.Count > 0) _paletteFiltered[_paletteIndex].Execute();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClosePalette();
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (PaletteOverlay.Visibility == Visibility.Visible) ClosePalette(); else OpenPalette();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && PaletteOverlay.Visibility == Visibility.Visible)
        {
            ClosePalette();
            e.Handled = true;
        }
    }
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
