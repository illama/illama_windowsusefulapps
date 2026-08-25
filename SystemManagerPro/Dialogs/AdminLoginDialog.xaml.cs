using System.Windows;
using System.Windows.Input;
using SystemManagerPro.Services;

namespace SystemManagerPro.Dialogs;

public partial class AdminLoginDialog : Window
{
    public AdminLoginDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => UserBox.Focus();
    }

    private void Login_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void PassBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        if (LicenseService.Instance.TryAdminLogin(UserBox.Text, PassBox.Password))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ErrorText.Text = "Identifiant ou mot de passe incorrect.";
            ErrorText.Visibility = Visibility.Visible;
            PassBox.Clear();
            PassBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Affiche la boîte de connexion admin ; renvoie true si l'authentification a réussi.</summary>
    public static bool ShowLogin(Window owner)
    {
        var dlg = new AdminLoginDialog { Owner = owner };
        return dlg.ShowDialog() == true;
    }
}
