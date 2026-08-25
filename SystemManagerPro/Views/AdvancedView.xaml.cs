using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class AdvancedView : UserControl, IActivatable
{
    private readonly EnvironmentVariableService _envService = new();
    private readonly HostsFileService _hostsService = new();
    private readonly PowerPlanService _powerService = new();
    private readonly FirewallService _firewallService = new();

    private List<string> _hostsLines = new();
    private bool _hostsDirty;
    private bool _loadedOnce;

    public AdvancedView()
    {
        InitializeComponent();
    }

    public void OnActivated() { if (_loadedOnce) RefreshCurrentTab(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        RefreshEnvVars();
    }

    private void RefreshCurrentTab()
    {
        if (TabEnv.IsChecked == true) RefreshEnvVars();
        else if (TabHosts.IsChecked == true) RefreshHosts();
        else if (TabPower.IsChecked == true) RefreshPower();
        else RefreshFirewall();
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        EnvPanel.Visibility = TabEnv.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        HostsPanel.Visibility = TabHosts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PowerPanel.Visibility = TabPower.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        FirewallPanel.Visibility = TabFirewall.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RefreshCurrentTab();
    }

    // ===================== Variables d'environnement =====================

    private EnvironmentVariableTarget CurrentScope =>
        ScopeMachine.IsChecked == true ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;

    private void Scope_Checked(object sender, RoutedEventArgs e) { if (IsLoaded) RefreshEnvVars(); }

    private void RefreshEnvVars()
    {
        try { EnvGrid.ItemsSource = _envService.GetAll(CurrentScope); }
        catch (Exception ex) { LogService.Instance.Log("Erreur lors de la lecture des variables : " + ex.Message, LogLevel.Error); }
    }

    private void AddEnvVar_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = InputDialog.Ask(owner, "Ajouter une variable d'environnement", "Nom", "Valeur", showBrowse: false);
        if (result == null) return;
        var (name, value) = result.Value;
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            _envService.Set(name, value, CurrentScope);
            LogService.Instance.Log($"Variable « {name} » définie.", LogLevel.Success);
            RefreshEnvVars();
        }
        catch (Exception ex) { LogService.Instance.Log("Échec : " + ex.Message, LogLevel.Error); }
    }

    private void EditEnvVar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not EnvVarRow row) return;
        var owner = Window.GetWindow(this);
        var result = InputDialog.Ask(owner, $"Modifier « {row.Name} »", "Nom", "Valeur",
            field1Initial: row.Name, field2Initial: row.Value, field1ReadOnly: true, showBrowse: false);
        if (result == null) return;

        try
        {
            _envService.Set(row.Name, result.Value.Field2, CurrentScope);
            LogService.Instance.Log($"Variable « {row.Name} » mise à jour.", LogLevel.Success);
            RefreshEnvVars();
        }
        catch (Exception ex) { LogService.Instance.Log("Échec : " + ex.Message, LogLevel.Error); }
    }

    private void DeleteEnvVar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not EnvVarRow row) return;
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Supprimer la variable", $"Supprimer « {row.Name} » ?", "Supprimer", danger: true)) return;

        try
        {
            _envService.Delete(row.Name, CurrentScope);
            LogService.Instance.Log($"Variable « {row.Name} » supprimée.", LogLevel.Success);
            RefreshEnvVars();
        }
        catch (Exception ex) { LogService.Instance.Log("Échec : " + ex.Message, LogLevel.Error); }
    }

    // ===================== Fichier hosts =====================

    private void RefreshHosts()
    {
        try
        {
            _hostsLines = _hostsService.ReadLines();
            _hostsDirty = false;
            RenderHosts();
        }
        catch (Exception ex) { LogService.Instance.Log("Erreur lors de la lecture du fichier hosts : " + ex.Message, LogLevel.Error); }
    }

    private void RefreshHosts_Click(object sender, RoutedEventArgs e)
    {
        if (_hostsDirty)
        {
            var owner = Window.GetWindow(this);
            if (!ConfirmDialog.Ask(owner, "Annuler les modifications", "Des modifications non enregistrées seront perdues. Continuer ?", "Continuer"))
                return;
        }
        RefreshHosts();
    }

    private void RenderHosts()
    {
        HostsList.Items.Clear();
        var entries = _hostsService.ParseEntries(_hostsLines);

        if (entries.Count == 0)
        {
            HostsList.Items.Add(new TextBlock { Text = "Aucune entrée personnalisée.", Style = (Style)FindResource("Muted"), Margin = new Thickness(4) });
        }

        foreach (var entry in entries)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("BgElevated2"),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var toggle = new CheckBox { Style = (Style)FindResource("ToggleSwitch"), IsChecked = entry.Enabled, VerticalAlignment = VerticalAlignment.Center };
            toggle.Click += (_, _) => { _hostsService.ToggleEntry(_hostsLines, entry.LineIndex); MarkDirty(); RenderHosts(); };

            var text = new TextBlock
            {
                Text = $"{entry.Ip}   →   {entry.Hostname}", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                Opacity = entry.Enabled ? 1 : 0.5,
            };

            var removeBtn = new Button { Content = "Retirer", Style = (Style)FindResource("BtnGhost") };
            removeBtn.Click += (_, _) => { _hostsService.RemoveEntry(_hostsLines, entry.LineIndex); MarkDirty(); RenderHosts(); };

            Grid.SetColumn(text, 1);
            Grid.SetColumn(removeBtn, 2);
            grid.Children.Add(toggle);
            grid.Children.Add(text);
            grid.Children.Add(removeBtn);
            row.Child = grid;
            HostsList.Items.Add(row);
        }
    }

    private void MarkDirty()
    {
        _hostsDirty = true;
        HostsStatusText.Text = "Modifications non enregistrées.";
    }

    private void AddHostEntry_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = InputDialog.Ask(owner, "Ajouter une entrée hosts", "Adresse IP (ex : 127.0.0.1)", "Nom d'hôte (ex : exemple.com)", showBrowse: false);
        if (result == null) return;
        var (ip, host) = result.Value;
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(host)) return;

        _hostsService.AddEntry(_hostsLines, ip, host);
        MarkDirty();
        RenderHosts();
    }

    private void SaveHosts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _hostsService.WriteLines(_hostsLines);
            _hostsDirty = false;
            HostsStatusText.Text = "Enregistré.";
            LogService.Instance.Log("Fichier hosts mis à jour.", LogLevel.Success);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Échec de l'enregistrement du fichier hosts : " + ex.Message, LogLevel.Error);
        }
    }

    // ===================== Alimentation =====================

    private void RefreshPower()
    {
        try
        {
            var plans = _powerService.GetPlans();
            PowerList.Items.Clear();
            foreach (var plan in plans)
            {
                var row = new Border
                {
                    Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 8),
                    CornerRadius = new CornerRadius(8),
                    Background = plan.Active ? (Brush)FindResource("BgElevated3") : (Brush)FindResource("BgElevated2"),
                    BorderBrush = plan.Active ? (Brush)FindResource("Accent") : (Brush)FindResource("BorderBrush2"),
                    BorderThickness = new Thickness(1),
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var text = new TextBlock { Text = plan.Name, FontSize = 13, FontWeight = plan.Active ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
                grid.Children.Add(text);

                if (plan.Active)
                {
                    var badge = new Border { Background = (Brush)FindResource("Success"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 3, 8, 3) };
                    badge.Child = new TextBlock { Text = "Actif", FontSize = 10.5, Foreground = Brushes.White };
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge);
                }
                else
                {
                    var btn = new Button { Content = "Activer", Style = (Style)FindResource("BtnBase"), Padding = new Thickness(12, 5, 12, 5) };
                    btn.Click += (_, _) =>
                    {
                        var (ok, message) = _powerService.Activate(plan.Guid);
                        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
                        RefreshPower();
                    };
                    Grid.SetColumn(btn, 1);
                    grid.Children.Add(btn);
                }

                row.Child = grid;
                PowerList.Items.Add(row);
            }
        }
        catch (Exception ex) { LogService.Instance.Log("Erreur lors de la lecture des modes d'alimentation : " + ex.Message, LogLevel.Error); }
    }

    private void RefreshPower_Click(object sender, RoutedEventArgs e) => RefreshPower();

    // ===================== Pare-feu =====================

    private void RefreshFirewall()
    {
        try
        {
            var states = _firewallService.GetStates();
            FirewallList.Items.Clear();
            foreach (var state in states)
            {
                var row = new Border
                {
                    Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 8),
                    CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("BgElevated2"),
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel();
                left.Children.Add(new TextBlock { Text = $"Réseau {state.Profile}", FontSize = 13, FontWeight = FontWeights.SemiBold });
                left.Children.Add(new TextBlock
                {
                    Text = state.Enabled ? "Protégé" : "Non protégé",
                    Style = (Style)FindResource("Muted"),
                    Foreground = state.Enabled ? (Brush)FindResource("Success") : (Brush)FindResource("Danger"),
                    Margin = new Thickness(0, 2, 0, 0),
                });
                grid.Children.Add(left);

                var toggle = new CheckBox { Style = (Style)FindResource("ToggleSwitch"), IsChecked = state.Enabled, VerticalAlignment = VerticalAlignment.Center };
                toggle.Click += (_, _) =>
                {
                    bool desired = toggle.IsChecked == true;
                    var (ok, message) = _firewallService.SetProfile(state.Profile, desired);
                    LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
                    if (!ok) toggle.IsChecked = state.Enabled; // annule visuellement si l'appel a échoué
                    else RefreshFirewall();
                };
                Grid.SetColumn(toggle, 1);
                grid.Children.Add(toggle);

                row.Child = grid;
                FirewallList.Items.Add(row);
            }
        }
        catch (Exception ex) { LogService.Instance.Log("Erreur lors de la lecture du pare-feu : " + ex.Message, LogLevel.Error); }
    }

    private void RefreshFirewall_Click(object sender, RoutedEventArgs e) => RefreshFirewall();
}
