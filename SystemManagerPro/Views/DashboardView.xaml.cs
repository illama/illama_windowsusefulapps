using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class DashboardView : UserControl, IActivatable
{
    private const int HistoryLength = 24;

    private readonly SystemInfoService _sysInfo = new();
    private readonly NetworkService _network = new();
    private readonly TweaksService _tweaks = new();
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();
    private DispatcherTimer? _timer;

    public DashboardView()
    {
        InitializeComponent();
    }

    public void OnActivated() => Refresh();

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Refresh();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
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
        SystemSnapshot snap;
        try { snap = _sysInfo.GetSnapshot(); }
        catch { return; }

        CpuValue.Text = $"{snap.CpuPercent:0}%";
        AnimateBar(CpuBar, snap.CpuPercent);
        PushHistory(_cpuHistory, snap.CpuPercent);
        DrawSparkline(CpuSparkline, _cpuHistory);

        RamValue.Text = $"{snap.RamPercent:0}%";
        AnimateBar(RamBar, snap.RamPercent);
        RamDetail.Text = $"{snap.RamUsedGb:0.#} Go / {snap.RamTotalGb:0.#} Go";
        PushHistory(_ramHistory, snap.RamPercent);
        DrawSparkline(RamSparkline, _ramHistory);

        var sysDisk = snap.Disks.FirstOrDefault(d => d.Label.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                      ?? snap.Disks.FirstOrDefault();
        if (sysDisk != null)
        {
            DiskValue.Text = $"{sysDisk.Percent:0}%";
            DiskBar.Value = sysDisk.Percent;
            DiskDetail.Text = $"{sysDisk.UsedGb:0.#} Go / {sysDisk.TotalGb:0.#} Go";
        }

        UptimeValue.Text = FormatUptime(snap.Uptime);

        InfoStack.Children.Clear();
        AddInfoRow("Ordinateur", snap.ComputerName);
        AddInfoRow("Utilisateur", snap.UserName);
        AddInfoRow("Système", snap.OsCaption);
        AddInfoRow("Droits admin", snap.IsAdmin ? "Actif ✓" : "Inactif", snap.IsAdmin);

        DisksStack.Children.Clear();
        foreach (var d in snap.Disks)
            AddDiskRow(d);
    }

    private void AddInfoRow(string label, string value, bool? highlightGood = null)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock { Text = label, Foreground = (Brush)FindResource("TextSecondary"), FontSize = 13 };
        var val = new TextBlock
        {
            Text = value,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = highlightGood == false ? (Brush)FindResource("Danger")
                       : highlightGood == true ? (Brush)FindResource("Success")
                       : (Brush)FindResource("TextPrimary")
        };
        Grid.SetColumn(val, 1);
        row.Children.Add(lbl);
        row.Children.Add(val);
        InfoStack.Children.Add(row);
    }

    private void AddDiskRow(DiskSnapshot d)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = $"Disque {d.Label}", FontSize = 13, FontWeight = FontWeights.SemiBold });
        var right = new TextBlock
        {
            Text = $"{d.UsedGb:0.#} / {d.TotalGb:0.#} Go",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondary")
        };
        Grid.SetColumn(right, 1);
        header.Children.Add(right);
        stack.Children.Add(header);
        stack.Children.Add(new ProgressBar { Value = d.Percent, Maximum = 100, Margin = new Thickness(0, 8, 0, 0) });
        DisksStack.Children.Add(stack);
    }

    private static void AnimateBar(ProgressBar bar, double value)
    {
        var anim = new DoubleAnimation(bar.Value, value, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        bar.BeginAnimation(RangeBase.ValueProperty, anim);
    }

    private void PushHistory(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > HistoryLength) history.Dequeue();
    }

    private static void DrawSparkline(Polyline line, Queue<double> history)
    {
        if (history.Count < 2 || line.ActualWidth <= 0)
        {
            // La largeur réelle n'est pas encore connue au tout premier rafraîchissement : on retentera au suivant.
            line.Points.Clear();
            return;
        }

        double width = line.ActualWidth;
        double height = 28; // hauteur du conteneur (Border Height="30" - petite marge)
        var values = history.ToArray();
        var points = new PointCollection();
        for (int i = 0; i < values.Length; i++)
        {
            double x = values.Length == 1 ? width : i * (width / (values.Length - 1));
            double y = height - (values[i] / 100.0 * height);
            points.Add(new Point(x, y));
        }
        line.Points = points;
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} j {ts.Hours} h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} h {ts.Minutes} min";
        return $"{ts.Minutes} min";
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        var result = _network.FlushDns();
        LogService.Instance.Log(
            result.ExitCode == 0 ? "Cache DNS vidé avec succès." : "Échec du vidage du cache DNS.",
            result.ExitCode == 0 ? LogLevel.Success : LogLevel.Error);
    }

    private void RestorePoint_Click(object sender, RoutedEventArgs e)
    {
        var (ok, message) = _tweaks.CreateRestorePoint("Gestionnaire Système Pro - Point manuel");
        LogService.Instance.Log(message, ok ? LogLevel.Success : LogLevel.Error);
    }
}
