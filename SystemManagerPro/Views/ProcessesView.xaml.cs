using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class ProcessesView : UserControl, IActivatable
{
    private readonly ProcessManagerService _service = new();
    private List<ProcessRow> _all = new();
    private DispatcherTimer? _timer;

    public ProcessesView()
    {
        InitializeComponent();
    }

    public void OnActivated() => Refresh();

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Refresh(); // premier appel : établit la ligne de base pour le calcul du CPU%
        Refresh(); // second appel immédiat : donne un premier pourcentage exploitable
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Refresh()
    {
        try { _all = _service.GetSnapshot(); }
        catch { return; }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || p.Pid.ToString().Contains(term)).ToList();
        ProcessGrid.ItemsSource = filtered;

        StatsPanel.Children.Clear();
        StatsPanel.Children.Add(UiHelpers.Chip("processus", _all.Count.ToString(), (Brush)FindResource("Accent"), this));
        double totalMem = Math.Round(_all.Sum(p => p.MemoryMb) / 1024, 1);
        StatsPanel.Children.Add(UiHelpers.Chip("Go de RAM utilisés", totalMem.ToString("0.#"), (Brush)FindResource("Warning"), this));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void EndTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ProcessRow row) return;

        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Arrêter le processus",
            $"Forcer l'arrêt de « {row.Name} » (PID {row.Pid}) ?\nToute donnée non enregistrée dans ce programme sera perdue.",
            "Arrêter", danger: true))
            return;

        var (ok, message) = _service.EndTask(row.Pid);
        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
        Refresh();
    }
}
