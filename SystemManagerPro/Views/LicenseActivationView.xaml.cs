using System.Windows;
using System.Windows.Controls;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class LicenseActivationView : UserControl
{
    /// <summary>Déclenché après une activation (licence ou connexion admin) réussie, pour que
    /// la fenêtre principale puisse renvoyer l'utilisateur vers la page qu'il voulait ouvrir.</summary>
    public event Action? Unlocked;

    public LicenseActivationView()
    {
        InitializeComponent();
    }

    public void SetContext(string featureLabel)
    {
        MessageText.Text = $"La fonctionnalité « {featureLabel} » nécessite une licence Gestionnaire Système Pro.";
        StatusText.Text = "";
        KeyBox.Text = "";
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            StatusText.Text = "Entrez une clé de licence.";
            return;
        }

        var (ok, message) = LicenseService.Instance.Activate(key);
        StatusText.Text = message;
        if (ok)
        {
            LogService.Instance.Log(message, LogLevel.Success);
            Unlocked?.Invoke();
        }
        else
        {
            LogService.Instance.Log("Échec de l'activation de licence : " + message, LogLevel.Warning);
        }
    }

    private void AdminLogin_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (AdminLoginDialog.ShowLogin(owner))
        {
            LogService.Instance.Log("Connexion administrateur réussie.", LogLevel.Success);
            Unlocked?.Invoke();
        }
    }
}
