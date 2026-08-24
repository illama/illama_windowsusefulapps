using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class LanguageView : UserControl, IActivatable
{
    private readonly LanguageService _service = new();
    private List<LanguageEntry> _languages = new();
    private bool _loadedOnce;

    public LanguageView()
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
            _languages = await Task.Run(() => _service.GetInstalled());
            foreach (var lang in _languages) lang.Keep = true; // par défaut, tout est conservé
            LangList.ItemsSource = _languages;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Impossible de lire les langues installées : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Refresh();

    private void CheckAll_Click(object sender, RoutedEventArgs e) { foreach (var l in _languages) l.Keep = true; }
    private void UncheckAll_Click(object sender, RoutedEventArgs e) { foreach (var l in _languages) l.Keep = false; }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var toKeep = _languages.Where(l => l.Keep).ToList();
        var toRemove = _languages.Where(l => !l.Keep).ToList();

        if (toKeep.Count == 0)
        {
            LogService.Instance.Log("Il faut conserver au moins une langue.", LogLevel.Warning);
            return;
        }
        if (toRemove.Count == 0)
        {
            LogService.Instance.Log("Aucune langue à supprimer : toutes sont cochées.", LogLevel.Info);
            return;
        }

        var owner = Window.GetWindow(this);
        var names = string.Join("\n • ", toRemove.Select(l => $"{l.DisplayName} ({l.Tag})"));
        bool ok = ConfirmDialog.Ask(owner, "Supprimer des langues",
            $"Les langues suivantes seront supprimées définitivement :\n\n • {names}\n\nUn redémarrage sera nécessaire.",
            "Supprimer", danger: true);
        if (!ok) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await Task.Run(() => _service.ApplyKeepOnly(toKeep.Select(l => l.Tag)));
            if (HardeningCheck.IsChecked == true)
                await Task.Run(() => _service.ApplyHardeningTweaks());

            LogService.Instance.Log(
                $"Langues mises à jour : {toRemove.Count} supprimée(s), {toKeep.Count} conservée(s). Redémarrez pour finaliser.",
                LogLevel.Success);

            var restart = ConfirmDialog.Ask(owner, "Redémarrage requis",
                "Voulez-vous redémarrer l'ordinateur maintenant pour appliquer les changements ?", "Redémarrer maintenant");
            if (restart)
                System.Diagnostics.Process.Start("shutdown", "/r /t 10");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Échec de la mise à jour des langues : " + ex.Message, LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }
}
