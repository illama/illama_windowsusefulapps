using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class CleanupView : UserControl, IActivatable
{
    private readonly CleanupService _service = new();
    private List<CleanupCategory> _categories = new();
    private bool _loadedOnce;

    private record ResultRow(string Categorie, string FreedLabel);

    public CleanupView()
    {
        InitializeComponent();
    }

    public void OnActivated() { if (_loadedOnce) UpdateTotals(); }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        await Scan();
    }

    private async Task Scan()
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            _categories = await Task.Run(() => _service.BuildCategories());
            foreach (var cat in _categories) cat.PropertyChanged += (_, _) => UpdateTotals();
            CategoryList.ItemsSource = _categories;
            UpdateTotals();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du scan : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void UpdateTotals()
    {
        var selected = _categories.Where(c => c.IsChecked).ToList();
        long total = selected.Sum(c => c.TailleBytes);
        TotalLabel.Text = CleanupCategory.FormatBytes(total);
        SelectedCountLabel.Text = $"{selected.Count} / {_categories.Count}";
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await Scan();

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = _categories.Where(c => c.IsChecked).ToList();
        if (selected.Count == 0)
        {
            LogService.Instance.Log("Aucune catégorie sélectionnée.", LogLevel.Warning);
            return;
        }

        var owner = Window.GetWindow(this);
        long total = selected.Sum(c => c.TailleBytes);
        bool ok = ConfirmDialog.Ask(owner, "Nettoyer le système",
            $"Supprimer {selected.Count} catégorie(s) et libérer environ {CleanupCategory.FormatBytes(total)} ?", "Nettoyer");
        if (!ok) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var results = await Task.Run(() => _service.Clean(selected));
            ResultList.ItemsSource = results.Select(r =>
                new ResultRow(r.Categorie, $"{CleanupCategory.FormatBytes(r.OctetsLiberes)} libérés" +
                    (r.Erreurs > 0 ? $" ({r.Erreurs} erreur(s))" : ""))).ToList();
            ResultCard.Visibility = Visibility.Visible;

            long freed = results.Sum(r => r.OctetsLiberes);
            LogService.Instance.Log($"Nettoyage terminé : {CleanupCategory.FormatBytes(freed)} libérés.", LogLevel.Success);

            await Scan();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du nettoyage : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }
}
