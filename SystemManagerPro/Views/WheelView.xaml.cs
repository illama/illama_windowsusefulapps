using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class WheelView : UserControl, IActivatable
{
    private readonly WheelPowerService _wheel = WheelPowerService.Instance;
    private bool _loadedOnce;
    private bool _suppressEvents;

    // Compteurs locaux de la zone de test (remis à zéro via le bouton "Réinitialiser").
    private int _testNotches;
    private double _testLines;

    public WheelView()
    {
        InitializeComponent();
    }

    public void OnActivated() { if (_loadedOnce) RefreshAll(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_loadedOnce)
        {
            _loadedOnce = true;
            _wheel.Boosted += Wheel_Boosted;
            Unloaded += (_, _) => _wheel.Boosted -= Wheel_Boosted;
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        _suppressEvents = true;

        TabGlobal.IsChecked = _wheel.Current.Mode == WheelPowerMode.Global;
        TabApp.IsChecked = _wheel.Current.Mode == WheelPowerMode.SpecificApp;
        GlobalPanel.Visibility = TabGlobal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AppPanel.Visibility = TabApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        VerticalSlider.Value = _wheel.GetVerticalLines();
        HorizontalSlider.Value = _wheel.GetHorizontalChars();
        UpdateVerticalTexts();
        UpdateHorizontalTexts();

        TargetPathText.Text = string.IsNullOrWhiteSpace(_wheel.Current.TargetPath)
            ? "Aucune application choisie."
            : (_wheel.Current.TargetIsFolder ? "📁 " : "📄 ") + _wheel.Current.TargetPath;
        MultiplierSlider.Value = _wheel.Current.Multiplier;
        UpdateMultiplierTexts();
        AppEnabledToggle.IsChecked = _wheel.IsAppModeRunning;
        UpdateAppStatusText();
        TotalBoostedValue.Text = _wheel.TotalBoostedEvents.ToString();

        _suppressEvents = false;
    }

    // ===================== Onglets =====================

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        GlobalPanel.Visibility = TabGlobal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AppPanel.Visibility = TabApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _wheel.SetMode(TabGlobal.IsChecked == true ? WheelPowerMode.Global : WheelPowerMode.SpecificApp);
        if (TabApp.IsChecked == true) { AppEnabledToggle.IsChecked = _wheel.IsAppModeRunning; UpdateAppStatusText(); }
    }

    // ===================== Mode global =====================

    private static int PercentOf(int value) => (int)Math.Round(value * 100.0 / WheelPowerService.MaxSteps);

    private void UpdateVerticalTexts()
    {
        int lines = (int)Math.Round(VerticalSlider.Value);
        int pct = PercentOf(lines);
        VerticalValueText.Text = $"{lines} ligne{(lines > 1 ? "s" : "")} • {pct}%";
        VerticalMeter.Value = pct;
        TestPowerValue.Text = $"{pct}%";
    }

    private void UpdateHorizontalTexts()
    {
        int chars = (int)Math.Round(HorizontalSlider.Value);
        int pct = PercentOf(chars);
        HorizontalValueText.Text = $"{chars} caractère{(chars > 1 ? "s" : "")} • {pct}%";
        HorizontalMeter.Value = pct;
    }

    private void VerticalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || !IsLoaded) return;
        _wheel.SetVerticalLines((int)Math.Round(e.NewValue));
        UpdateVerticalTexts();
    }

    private void HorizontalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || !IsLoaded) return;
        _wheel.SetHorizontalChars((int)Math.Round(e.NewValue));
        UpdateHorizontalTexts();
    }

    private void ResetGlobalDefaults_Click(object sender, RoutedEventArgs e)
    {
        _wheel.ResetGlobalDefaults();
        _suppressEvents = true;
        VerticalSlider.Value = _wheel.GetVerticalLines();
        HorizontalSlider.Value = _wheel.GetHorizontalChars();
        _suppressEvents = false;
        UpdateVerticalTexts();
        UpdateHorizontalTexts();
        LogService.Instance.Log("Défilement de la molette réinitialisé aux valeurs par défaut de Windows.", LogLevel.Success);
    }

    // ===================== Zone de test =====================

    private void TestZone_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true; // capture le scroll ici : n'affecte pas le reste de la page pendant le test
        int currentLines = (int)Math.Round(VerticalSlider.Value);
        int notches = Math.Max(1, Math.Abs(e.Delta) / 120);
        _testNotches += notches;
        _testLines += notches * currentLines;

        TestZoneIcon.Text = e.Delta > 0 ? "⬆️" : "⬇️";
        TestZoneText.Text = e.Delta > 0 ? "Défilement vers le haut détecté" : "Défilement vers le bas détecté";
        TestNotchesValue.Text = _testNotches.ToString();
        TestLinesValue.Text = Math.Round(_testLines).ToString();
    }

    private void ResetTestStats_Click(object sender, RoutedEventArgs e)
    {
        _testNotches = 0;
        _testLines = 0;
        TestNotchesValue.Text = "0";
        TestLinesValue.Text = "0";
        TestZoneIcon.Text = "🖱️";
        TestZoneText.Text = "Scrollez ici pour tester…";
    }

    // ===================== Mode application spécifique =====================

    private void UpdateMultiplierTexts()
    {
        double m = MultiplierSlider.Value;
        int pct = (int)Math.Round(m * 100);
        MultiplierValueText.Text = $"x{m:0.0#} • {pct}%";
        MultiplierPercentValue.Text = $"{pct}%";
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || !IsLoaded) return;
        _wheel.SetMultiplier(e.NewValue);
        UpdateMultiplierTexts();
    }

    private void ChooseExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Exécutables (*.exe)|*.exe|Tous les fichiers (*.*)|*.*", Title = "Choisir l'application à cibler" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        _wheel.SetTarget(dlg.FileName, isFolder: false);
        TargetPathText.Text = "📄 " + dlg.FileName;
        LogService.Instance.Log($"Application ciblée pour la molette : {dlg.FileName}", LogLevel.Info);
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        // FolderBrowserDialog (WinForms) : pas d'équivalent natif WPF, mais UseWindowsForms est déjà
        // référencé dans le projet pour l'icône de la zone de notification — type entièrement qualifié
        // pour éviter toute ambiguïté avec les usings globaux WPF (voir le commentaire dans le .csproj).
        using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choisir le dossier à cibler" };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        _wheel.SetTarget(dlg.SelectedPath, isFolder: true);
        TargetPathText.Text = "📁 " + dlg.SelectedPath;
        LogService.Instance.Log($"Dossier ciblé pour la molette : {dlg.SelectedPath}", LogLevel.Info);
    }

    private void UpdateAppStatusText()
    {
        AppStatusText.Text = _wheel.IsAppModeRunning
            ? "Activée — la molette est amplifiée au-dessus de l'application ciblée."
            : "Désactivée.";
    }

    private void AppEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        bool desired = AppEnabledToggle.IsChecked == true;
        var (ok, message) = _wheel.SetAppModeEnabled(desired);
        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Warning);
        if (!ok) AppEnabledToggle.IsChecked = _wheel.IsAppModeRunning;
        UpdateAppStatusText();
    }

    private void Wheel_Boosted(WheelBoostStat stat)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LiveStatText.Text = $"Dernier scroll amplifié sur « {stat.ProcessName} » : delta {stat.OriginalDelta} → {stat.AppliedDelta}.";
            TotalBoostedValue.Text = stat.TotalBoosted.ToString();
        });
    }
}
