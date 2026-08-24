using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class DashboardView : UserControl, IActivatable
{
    private readonly SystemInfoService _sysInfo = new();
    private readonly NetworkService _network = new();
    private readonly TweaksService _tweaks = new();
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
        CpuBar.Value = snap.CpuPercent;

        RamValue.Text = $"{snap.RamPercent:0}%";
        RamBar.Value = snap.RamPercent;
        RamDetail.Text = $"{snap.RamUsedGb:0.#} Go / {snap.RamTotalGb:0.#} Go";

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
