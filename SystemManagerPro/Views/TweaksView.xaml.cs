using System.Windows;
using System.Windows.Controls;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class TweaksView : UserControl, IActivatable
{
    private readonly TweaksService _service = new();
    private List<QuickTweak> _tweaks = new();
    private bool _loadedOnce;

    public TweaksView()
    {
        InitializeComponent();
    }

    public void OnActivated() { if (_loadedOnce) LoadTweaks(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        LoadTweaks();
    }

    private void LoadTweaks()
    {
        _tweaks = _service.BuildTweaks();
        foreach (var t in _tweaks)
        {
            try { t.IsOn = t.Getter?.Invoke() ?? false; }
            catch { t.IsOn = false; }
        }
        TweakList.ItemsSource = _tweaks;
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not QuickTweak tweak) return;
        bool desired = cb.IsChecked == true;
        try
        {
            tweak.Setter?.Invoke(desired);
            tweak.IsOn = desired;
            LogService.Instance.Log($"« {tweak.Nom} » {(desired ? "activé" : "désactivé")}.", LogLevel.Success);
        }
        catch (Exception ex)
        {
            cb.IsChecked = tweak.IsOn;
            LogService.Instance.Log($"Échec du réglage « {tweak.Nom} » : {ex.Message}", LogLevel.Error);
        }
    }

    private void RestorePoint_Click(object sender, RoutedEventArgs e)
    {
        var (ok, message) = _service.CreateRestorePoint("Gestionnaire Système Pro - Point manuel");
        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
    }
}
