using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class InstallerView : UserControl, IActivatable
{
    private readonly AppInstallerService _service = new();
    private List<InstallableApp> _apps = new();
    private string _categoryFilter = "Tous";
    private bool _loadedOnce;
    private bool _installing;

    public InstallerView()
    {
        InitializeComponent();
    }

    public void OnActivated() { /* le catalogue est statique : pas besoin de recharger à chaque activation */ }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;

        _apps = AppInstallerService.Catalog.Select(c => new InstallableApp
        {
            WingetId = c.WingetId, Name = c.Name, Category = c.Category, Description = c.Description, Icon = c.Icon,
        }).ToList();

        BuildCategoryPills();
        RenderCatalog();

        bool wingetOk = await Task.Run(() => _service.IsWingetAvailable());
        WingetMissingCard.Visibility = wingetOk ? Visibility.Collapsed : Visibility.Visible;
        InstallSelectionBtn.IsEnabled = wingetOk;
    }

    private void BuildCategoryPills()
    {
        CategoryPills.Children.Clear();
        var categories = new[] { "Tous" }.Concat(_apps.Select(a => a.Category).Distinct()).ToList();
        foreach (var cat in categories)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("PillTab"),
                GroupName = "InstallerCat",
                Content = cat,
                IsChecked = cat == "Tous",
                Margin = new Thickness(0, 0, 8, 0),
            };
            rb.Checked += (_, _) => { _categoryFilter = cat; RenderCatalog(); };
            CategoryPills.Children.Add(rb);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderCatalog();

    private void RenderCatalog()
    {
        CatalogStack.Children.Clear();
        var term = SearchBox.Text?.Trim() ?? "";

        var filtered = _apps.Where(a =>
            (_categoryFilter == "Tous" || a.Category == _categoryFilter) &&
            (string.IsNullOrEmpty(term) || a.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                                         || a.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));

        var groups = filtered.GroupBy(a => a.Category).OrderBy(g => _apps.FindIndex(a => a.Category == g.Key));

        bool any = false;
        foreach (var group in groups)
        {
            any = true;
            CatalogStack.Children.Add(new TextBlock
            {
                Text = group.Key, Style = (Style)FindResource("SectionTitle"), Margin = new Thickness(2, 18, 0, 10),
            });
            var wrap = new WrapPanel();
            foreach (var app in group) wrap.Children.Add(BuildCard(app));
            CatalogStack.Children.Add(wrap);
        }

        if (!any)
        {
            CatalogStack.Children.Add(new TextBlock
            {
                Text = "Aucune application ne correspond à votre recherche.",
                Style = (Style)FindResource("Muted"), Margin = new Thickness(2, 20, 0, 0),
            });
        }
    }

    private Border BuildCard(InstallableApp app)
    {
        var card = new Border
        {
            Style = (Style)FindResource("CardHover"),
            Width = 258,
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(14),
        };

        var root = new StackPanel();

        var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var checkBox = new CheckBox { Style = (Style)FindResource("SquareCheckBox"), VerticalAlignment = VerticalAlignment.Center };
        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(InstallableApp.IsChecked)) { Source = app, Mode = BindingMode.TwoWay });
        header.Children.Add(checkBox);
        header.Children.Add(new TextBlock { Text = app.Icon, FontSize = 20, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = app.Name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 150 });
        root.Children.Add(header);

        root.Children.Add(new TextBlock
        {
            Text = app.Description, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap,
        });

        var statusText = new TextBlock { FontSize = 11, Margin = new Thickness(0, 10, 0, 0), FontWeight = FontWeights.SemiBold };
        statusText.SetBinding(TextBlock.TextProperty, new Binding(nameof(InstallableApp.StatusLabel)) { Source = app });
        // Couleur du statut : vert si installé/succès, rouge si échec, accent si en cours.
        void UpdateStatusColor()
        {
            statusText.Foreground = app.Status.Contains("Échec", StringComparison.OrdinalIgnoreCase)
                ? (Brush)FindResource("Danger")
                : app.Status.Contains("Installation", StringComparison.OrdinalIgnoreCase)
                    ? (Brush)FindResource("Accent")
                    : (Brush)FindResource("Success");
        }
        app.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(InstallableApp.Status) or nameof(InstallableApp.IsInstalled)) UpdateStatusColor(); };
        UpdateStatusColor();
        root.Children.Add(statusText);

        card.Child = root;
        return card;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var installed = await _service.GetInstalledIdsAsync();
            foreach (var app in _apps)
                app.IsInstalled = installed.Contains(app.WingetId);
            LogService.Instance.Log($"Scan terminé : {installed.Count} application(s) du catalogue déjà installée(s).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du scan : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async void InstallSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_installing) return;
        var selected = _apps.Where(a => a.IsChecked).ToList();
        if (selected.Count == 0)
        {
            LogService.Instance.Log("Aucune application sélectionnée.", LogLevel.Warning);
            return;
        }

        var owner = Window.GetWindow(this);
        var names = string.Join(", ", selected.Select(a => a.Name));
        if (!ConfirmDialog.Ask(owner, "Installer les applications",
            $"Installer {selected.Count} application(s) via winget ?\n\n{names}\n\nCela peut prendre plusieurs minutes selon votre connexion.",
            "Installer"))
            return;

        _installing = true;
        InstallSelectionBtn.IsEnabled = false;
        int ok = 0, failed = 0;

        foreach (var app in selected)
        {
            app.Status = "⏳ Installation…";
            var (success, message) = await _service.InstallAsync(app.WingetId);
            if (success)
            {
                app.Status = "✅ Installé";
                app.IsChecked = false;
                app.IsInstalled = true;
                ok++;
                LogService.Instance.Log($"« {app.Name} » installé avec succès.", LogLevel.Success);
            }
            else
            {
                app.Status = "❌ " + message;
                failed++;
                LogService.Instance.Log($"Échec de l'installation de « {app.Name} » : {message}", LogLevel.Error);
            }
        }

        _installing = false;
        InstallSelectionBtn.IsEnabled = true;
        LogService.Instance.Log($"Installation terminée : {ok} réussie(s), {failed} échouée(s).",
            failed == 0 ? LogLevel.Success : LogLevel.Warning);
    }
}
