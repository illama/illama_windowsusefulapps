using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class NetworkView : UserControl, IActivatable
{
    private readonly NetworkService _service = new();
    private bool _loadedOnce;

    public NetworkView()
    {
        InitializeComponent();
    }

    public void OnActivated() { if (_loadedOnce) LoadAdapters(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        LoadAdapters();
    }

    private void LoadAdapters()
    {
        try { AdaptersGrid.ItemsSource = _service.GetAdapters(); }
        catch (Exception ex) { LogService.Instance.Log("Erreur réseau : " + ex.Message, LogLevel.Error); }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadAdapters();

    private async void FlushDns_Click(object sender, RoutedEventArgs e) => await RunTool(
        () => _service.FlushDns(), "Cache DNS vidé.");

    private async void ResetWinsock_Click(object sender, RoutedEventArgs e) => await RunTool(
        () => _service.ResetWinsock(), "Winsock réinitialisé — redémarrage recommandé.");

    private async void ResetTcpIp_Click(object sender, RoutedEventArgs e) => await RunTool(
        () => _service.ResetTcpIp(), "Pile TCP/IP réinitialisée — redémarrage recommandé.");

    private async void Renew_Click(object sender, RoutedEventArgs e) => await RunTool(
        () => _service.ReleaseRenew(), "Adresse IP renouvelée.");

    private async Task RunTool(Func<ProcessRunner.RunResult> action, string successMessage)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var result = await Task.Run(action);
            bool ok = result.ExitCode == 0;
            ToolOutput.Text = ok ? result.StdOut.Trim() : result.StdErr.Trim();
            LogService.Instance.Log(ok ? successMessage : "Échec de la commande réseau.", ok ? LogLevel.Success : LogLevel.Error);
            if (ok) LoadAdapters();
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async void Ping_Click(object sender, RoutedEventArgs e)
    {
        var host = PingHostBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var results = await _service.PingAsync(host);
            PingResults.ItemsSource = results.Select(r =>
                $"#{r.Sequence}  {r.Cible}  →  {(r.Ms >= 0 ? $"{r.Ms} ms" : r.Statut)}").ToList();

            double? avg = results.Where(r => r.Ms >= 0).Select(r => (double)r.Ms).DefaultIfEmpty(-1).Average();
            LogService.Instance.Log(
                avg >= 0 ? $"Ping vers {host} : moyenne {avg:0} ms." : $"Ping vers {host} a échoué.",
                avg >= 0 ? LogLevel.Success : LogLevel.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }
}
