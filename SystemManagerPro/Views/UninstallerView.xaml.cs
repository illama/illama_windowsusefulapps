using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class UninstallerView : UserControl, IActivatable
{
    private readonly UninstallerService _service = new();
    private List<InstalledProgram> _all = new();
    private bool _loadedOnce;

    public UninstallerView()
    {
        InitializeComponent();
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
            _all = await Task.Run(() => _service.GetInstalled());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Erreur lors du chargement des programmes : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void ApplyFilter()
    {
        var term = SearchBox.Text?.Trim() ?? "";
        ProgramsGrid.ItemsSource = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(p => p.Nom.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || p.Editeur.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Refresh();

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not InstalledProgram program) return;

        var owner = Window.GetWindow(this);
        bool ok = ConfirmDialog.Ask(owner, "Désinstaller le programme",
            $"Lancer la désinstallation de « {program.Nom} » ?\nL'assistant de désinstallation du programme va s'ouvrir.",
            "Désinstaller", danger: true);
        if (!ok) return;

        try
        {
            _service.Uninstall(program);
            LogService.Instance.Log($"Désinstallation de « {program.Nom} » lancée.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Échec du lancement de la désinstallation : " + ex.Message, LogLevel.Error);
        }
    }
}
