using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        LogList.ItemsSource = LogService.Instance.Entries;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"journal_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            Filter = "Fichier texte (*.txt)|*.txt"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            LogService.Instance.ExportTo(dlg.FileName);
            LogService.Instance.Log("Journal exporté vers " + dlg.FileName, Models.LogLevel.Success);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Échec de l'export : " + ex.Message, Models.LogLevel.Error);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => LogService.Instance.Clear();
}
