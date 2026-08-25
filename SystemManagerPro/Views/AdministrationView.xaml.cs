using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class AdministrationView : UserControl, IActivatable
{
    /// <summary>Déclenché après une déconnexion admin, pour que la fenêtre principale puisse
    /// retirer cette page de la navigation et revenir au tableau de bord.</summary>
    public event Action? LoggedOut;

    public AdministrationView()
    {
        InitializeComponent();
    }

    public void OnActivated() => Refresh();

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        RenderLedger();
        RenderDeviceLicense();
    }

    private void RenderLedger()
    {
        LedgerList.Items.Clear();
        var ledger = LicenseService.Instance.GetLedger();
        if (ledger.Count == 0)
        {
            LedgerList.Items.Add(new TextBlock { Text = "Aucune licence émise pour l'instant.", Style = (Style)FindResource("Muted") });
            return;
        }

        foreach (var record in ledger)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("BgElevated2"),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            string expiryLabel = record.Expiry is { } e ? e.ToString("dd/MM/yyyy") : "Illimitée";
            left.Children.Add(new TextBlock { Text = record.CustomerName, FontSize = 13, FontWeight = FontWeights.SemiBold });
            left.Children.Add(new TextBlock
            {
                Text = $"{record.MaxPcs} PC · expire le {expiryLabel} · émise le {record.IssuedAt:dd/MM/yyyy HH:mm}",
                Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 2, 0, 0),
            });
            grid.Children.Add(left);

            var copyBtn = new Button { Content = "📋 Copier", Style = (Style)FindResource("BtnBase"), Padding = new Thickness(10, 5, 10, 5) };
            copyBtn.Click += (_, _) =>
            {
                try { Clipboard.SetText(record.Key); LogService.Instance.Log("Clé copiée dans le presse-papiers.", LogLevel.Success); }
                catch (Exception ex) { LogService.Instance.Log("Échec de la copie : " + ex.Message, LogLevel.Error); }
            };
            Grid.SetColumn(copyBtn, 1);
            grid.Children.Add(copyBtn);

            row.Child = grid;
            LedgerList.Items.Add(row);
        }
    }

    private void RenderDeviceLicense()
    {
        var current = LicenseService.Instance.GetCurrentLicense();
        DeviceLicenseText.Text = current != null
            ? $"Licence active : « {current.CustomerName} », {current.MaxPcs} PC, expire le {current.ExpiryLabel}."
            : "Aucune licence activée sur cet appareil (accès total via la session administrateur en cours).";
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var name = CustomerBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            LogService.Instance.Log("Le nom du client est obligatoire.", LogLevel.Warning);
            return;
        }
        if (!int.TryParse(MaxPcsBox.Text.Trim(), out int maxPcs) || maxPcs < 1)
        {
            LogService.Instance.Log("Le nombre de PC doit être un entier positif.", LogLevel.Warning);
            return;
        }

        DateTime? expiry = (DurationCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "30 jours" => DateTime.Now.AddDays(30),
            "90 jours" => DateTime.Now.AddDays(90),
            "1 an" => DateTime.Now.AddYears(1),
            _ => null,
        };

        var key = LicenseService.Instance.GenerateLicense(name, maxPcs, expiry);
        GeneratedKeyBox.Text = key;
        ResultPanel.Visibility = Visibility.Visible;
        LogService.Instance.Log($"Licence générée pour « {name} ».", LogLevel.Success);
        RenderLedger();
    }

    private void CopyGenerated_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(GeneratedKeyBox.Text); LogService.Instance.Log("Clé copiée dans le presse-papiers.", LogLevel.Success); }
        catch (Exception ex) { LogService.Instance.Log("Échec de la copie : " + ex.Message, LogLevel.Error); }
    }

    private void RemoveDeviceLicense_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Retirer la licence", "Retirer la licence activée sur cet appareil ?", "Retirer", danger: true)) return;
        LicenseService.Instance.Deactivate();
        LogService.Instance.Log("Licence retirée de cet appareil.", LogLevel.Success);
        RenderDeviceLicense();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LicenseService.Instance.AdminLogout();
        LogService.Instance.Log("Déconnexion administrateur.", LogLevel.Info);
        LoggedOut?.Invoke();
    }
}
