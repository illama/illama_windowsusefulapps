using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class StartupView : UserControl, IActivatable
{
    private readonly StartupService _service = new();
    private readonly ObservableCollection<StartupApp> _view = new();
    private List<StartupApp> _all = new();
    private bool _loadedOnce;

    public StartupView()
    {
        InitializeComponent();
        AppsGrid.ItemsSource = _view;
    }

    public void OnActivated() { if (_loadedOnce) _ = Refresh(); }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        await Refresh();
    }

    private async Task Refresh()
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            _all = await Task.Run(() => _service.GetAll());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du chargement des applications au démarrage : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void ApplyFilter()
    {
        var term = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(a => a.Nom.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || a.Chemin.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        _view.Clear();
        foreach (var a in filtered) _view.Add(a);

        StatsPanel.Children.Clear();
        StatsPanel.Children.Add(UiHelpers.Chip("au total", _all.Count.ToString(), (Brush)FindResource("Accent"), this));
        StatsPanel.Children.Add(UiHelpers.Chip("actives", _all.Count(a => a.Actif).ToString(), (Brush)FindResource("Success"), this));
        StatsPanel.Children.Add(UiHelpers.Chip("désactivées", _all.Count(a => !a.Actif).ToString(), (Brush)FindResource("Danger"), this));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Refresh();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = InputDialog.Ask(owner, "Ajouter une application au démarrage",
            "Nom de l'entrée (ex : MonApp)", "Chemin complet de l'exécutable");
        if (result == null) return;

        var (name, path) = result.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            LogService.Instance.Log("Chemin de l'exécutable manquant : ajout annulé.", LogLevel.Warning);
            return;
        }

        try
        {
            _service.Add(name, path.Trim('"'));
            LogService.Instance.Log($"« {name} » ajouté au démarrage.", LogLevel.Success);
            _ = Refresh();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Échec de l'ajout : " + ex.Message, LogLevel.Error);
        }
    }

    private void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not StartupApp app) return;
        bool desired = cb.IsChecked == true;

        var owner = Window.GetWindow(this);
        if (!desired)
        {
            bool ok = ConfirmDialog.Ask(owner, "Désactiver l'application",
                $"Désactiver « {app.Nom} » au démarrage ?\nType : {app.TypeLabel}", "Désactiver");
            if (!ok) { cb.IsChecked = app.Actif; return; }
        }

        try
        {
            _service.SetEnabled(app, desired);
            LogService.Instance.Log(
                $"« {app.Nom} » {(desired ? "activée" : "désactivée")} au démarrage.", LogLevel.Success);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            cb.IsChecked = app.Actif;
            LogService.Instance.Log("Échec de la modification : " + ex.Message, LogLevel.Error);
        }
    }
}
