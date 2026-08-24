using System.Windows;
using Microsoft.Win32;

namespace SystemManagerPro.Dialogs;

public partial class InputDialog : Window
{
    private readonly bool _browseIsFile;

    public string Field1 => Field1Box.Text.Trim();
    public string Field2 => Field2Box.Text.Trim();

    public InputDialog(string title, string field1Label, string? field2Label = null, bool browseFile = true)
    {
        InitializeComponent();
        TitleText.Text = title;
        Field1Label.Text = field1Label;
        _browseIsFile = browseFile;

        if (field2Label == null)
        {
            Field2Panel.Visibility = Visibility.Collapsed;
        }
        else
        {
            Field2Label.Text = field2Label;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Exécutables (*.exe)|*.exe|Tous les fichiers (*.*)|*.*" };
        if (dlg.ShowDialog(this) == true) Field2Box.Text = dlg.FileName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Field1))
        {
            ErrorText.Text = "Ce champ est obligatoire.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Affiche la boîte de saisie ; renvoie (Field1, Field2) ou null si annulé.</summary>
    public static (string Field1, string Field2)? Ask(Window owner, string title, string field1Label, string? field2Label = null)
    {
        var dlg = new InputDialog(title, field1Label, field2Label) { Owner = owner };
        return dlg.ShowDialog() == true ? (dlg.Field1, dlg.Field2) : null;
    }
}
