using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class ServicesView : UserControl, IActivatable
{
    private readonly ServiceManagerService _service = new();
    private readonly ObservableCollection<ServiceInfo> _view = new();
    private List<ServiceInfo> _all = new();
    private bool _loadedOnce;

    public ServicesView()
    {
        InitializeComponent();
        ServicesGrid.ItemsSource = _view;
    }

    public void OnActivated() { if (_loadedOnce) _ = Refresh(); }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        await Refresh();
    }

    private string CurrentFilter =>
        FilterRunning.IsChecked == true ? "Running" : FilterStopped.IsChecked == true ? "Stopped" : "All";

    private async Task Refresh()
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var filter = CurrentFilter;
            _all = await Task.Run(() => _service.GetAll(filter));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du chargement des services : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void ApplyFilter()
    {
        var term = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(s => s.Nom.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || s.NomAffichage.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        _view.Clear();
        foreach (var s in filtered) _view.Add(s);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Refresh();
    private async void Filter_Checked(object sender, RoutedEventArgs e) { if (IsLoaded) await Refresh(); }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        // IsChecked="True" en XAML déclenche Checked pendant InitializeComponent(), avant que
        // ListPanel/AnalysisPanel (déclarés plus bas) ne soient câblés : on ignore ce cas, les
        // valeurs par défaut du XAML correspondent déjà à l'état initial voulu.
        if (!IsLoaded) return;
        bool listTab = TabList.IsChecked == true;
        ListPanel.Visibility = listTab ? Visibility.Visible : Visibility.Collapsed;
        AnalysisPanel.Visibility = listTab ? Visibility.Collapsed : Visibility.Visible;
        if (!listTab) _ = LoadAnalysis();
    }

    private async Task LoadAnalysis()
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var top = await Task.Run(() => _service.GetTopResourceServices());
            ResourceGrid.ItemsSource = top;

            RecommendedList.Items.Clear();
            foreach (var rec in ServiceManagerService.Recommandes)
                RecommendedList.Items.Add(await BuildRecommendedRowAsync(rec));
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors de l'analyse des services : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async Task<UIElement> BuildRecommendedRowAsync(RecommendedService rec)
    {
        var current = await Task.Run(() => _service.GetAll("All").FirstOrDefault(s => s.Nom == rec.Nom));

        var row = new Border
        {
            Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource("BgElevated2"),
        };
        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = rec.Affichage, FontWeight = FontWeights.SemiBold, FontSize = 13 });
        left.Children.Add(new TextBlock { Text = $"{rec.Nom} — {rec.Raison}", Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 3, 0, 0) });

        if (current == null)
        {
            left.Children.Add(new TextBlock { Text = "Non présent sur ce système", Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 2, 0, 0) });
            grid.Children.Add(left);
            row.Child = grid;
            return row;
        }

        var btn = new Button
        {
            Content = current.EnCours ? "Désactiver" : "Déjà arrêté",
            Style = (Style)FindResource(current.EnCours ? "BtnDanger" : "BtnGhost"),
            IsEnabled = current.EnCours,
            Tag = rec.Nom,
        };
        btn.Click += async (_, _) =>
        {
            var owner = Window.GetWindow(this);
            if (!ConfirmDialog.Ask(owner, "Désactiver le service", $"Arrêter et désactiver « {rec.Affichage} » ?", "Désactiver")) return;
            var stop = _service.Stop(rec.Nom);
            var mode = _service.SetStartMode(rec.Nom, "Disabled");
            LogService.Instance.Log(stop.Ok && mode.Ok ? $"« {rec.Affichage} » désactivé." : "Échec de la désactivation.",
                stop.Ok && mode.Ok ? LogLevel.Success : LogLevel.Error);
            await LoadAnalysis();
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(btn, 1);
        btn.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(left);
        grid.Children.Add(btn);
        row.Child = grid;
        return row;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServicesGrid.SelectedItem is ServiceInfo s)
        {
            SelectionLabel.Text = $"Sélection : {s.NomAffichage} ({s.Nom}) — {s.Statut}, démarrage {s.TypeDemarrage}";
            StartModeCombo.SelectedIndex = s.TypeDemarrage switch
            {
                "Automatique" => 0, "Manuel" => 1, "Désactivé" => 2, _ => -1
            };
        }
        else
        {
            SelectionLabel.Text = "Sélectionnez un service dans la liste ci-dessous pour agir dessus.";
        }
    }

    private ServiceInfo? Selected => ServicesGrid.SelectedItem as ServiceInfo;

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        var r = _service.Start(s.Nom);
        LogService.Instance.Log($"{s.NomAffichage} : {r.Message}", r.Ok ? LogLevel.Success : LogLevel.Error);
        await Refresh();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Arrêter le service", $"Arrêter « {s.NomAffichage} » ?", "Arrêter")) return;
        var r = _service.Stop(s.Nom);
        LogService.Instance.Log($"{s.NomAffichage} : {r.Message}", r.Ok ? LogLevel.Success : LogLevel.Error);
        await Refresh();
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        var r = _service.Restart(s.Nom);
        LogService.Instance.Log($"{s.NomAffichage} : {r.Message}", r.Ok ? LogLevel.Success : LogLevel.Error);
        await Refresh();
    }

    private async void ApplyStartMode_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        if (StartModeCombo.SelectedItem is not ComboBoxItem item) return;
        var mode = item.Content.ToString() switch
        {
            "Automatique" => "Automatic", "Manuel" => "Manual", "Désactivé" => "Disabled", _ => null
        };
        if (mode == null) return;

        var r = _service.SetStartMode(s.Nom, mode);
        LogService.Instance.Log($"{s.NomAffichage} : {r.Message}", r.Ok ? LogLevel.Success : LogLevel.Error);
        await Refresh();
    }
}
