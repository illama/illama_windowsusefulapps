using System.Windows;
using System.Windows.Media;

namespace SystemManagerPro.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string okLabel = "Confirmer", bool danger = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        OkBtn.Content = okLabel;
        if (danger)
        {
            IconBadge.Background = (Brush)FindResource("Danger");
            OkBtn.Style = (Style)FindResource("BtnDanger");
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Affiche la boîte de confirmation et renvoie true si l'utilisateur a validé.</summary>
    public static bool Ask(Window owner, string title, string message, string okLabel = "Confirmer", bool danger = false)
    {
        var dlg = new ConfirmDialog(title, message, okLabel, danger) { Owner = owner };
        return dlg.ShowDialog() == true;
    }
}
