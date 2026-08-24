using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemManagerPro.Dialogs;
using SystemManagerPro.Models;
using SystemManagerPro.Services;

namespace SystemManagerPro.Views;

public partial class KeyboardView : UserControl, IActivatable
{
    private readonly KeyboardRemapService _service = new();
    private readonly List<KeyMapping> _pending = new();
    private bool _loadedOnce;

    public KeyboardView()
    {
        InitializeComponent();
        SourceCombo.ItemsSource = KeyboardRemapService.AllKeys;
        DestCombo.ItemsSource = KeyboardRemapService.AllKeys;
        if (KeyboardRemapService.AllKeys.Length > 0)
        {
            SourceCombo.SelectedIndex = 0;
            DestCombo.SelectedIndex = 0;
        }
    }

    public void OnActivated() { if (_loadedOnce) LoadCurrent(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        LoadCurrent();
        RenderPending();
    }

    private void AddMapping_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedItem is not KeyOption src || DestCombo.SelectedItem is not KeyOption dst) return;

        if (_pending.Any(m => m.SourceCode == src.Code))
        {
            LogService.Instance.Log($"« {src.Name} » a déjà un mapping en attente.", LogLevel.Warning);
            return;
        }

        _pending.Add(new KeyMapping { SourceCode = src.Code, DestCode = dst.Code, SourceName = src.Name, DestName = dst.Name });
        RenderPending();
    }

    private void RenderPending()
    {
        PendingList.Items.Clear();
        foreach (var m in _pending) PendingList.Items.Add(BuildMappingRow(m.SourceName, m.DestName, () =>
        {
            _pending.Remove(m);
            RenderPending();
        }));
        PendingEmptyLabel.Visibility = _pending.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement BuildMappingRow(string source, string dest, Action onRemove)
    {
        var row = new Border
        {
            Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("BgElevated2"),
        };
        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock
        {
            Text = $"{source}  →  {dest}", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
        };
        var removeBtn = new Button { Content = "Retirer", Style = (Style)FindResource("BtnGhost") };
        removeBtn.Click += (_, _) => onRemove();

        System.Windows.Controls.Grid.SetColumn(removeBtn, 1);
        grid.Children.Add(text);
        grid.Children.Add(removeBtn);
        row.Child = grid;
        return row;
    }

    private void ApplyMappings_Click(object sender, RoutedEventArgs e)
    {
        if (_pending.Count == 0)
        {
            LogService.Instance.Log("Aucun mapping en attente à appliquer.", LogLevel.Warning);
            return;
        }

        var owner = Window.GetWindow(this);
        bool ok = ConfirmDialog.Ask(owner, "Appliquer le remapping",
            $"Appliquer {_pending.Count} mapping(s) de touches ? Un redémarrage sera nécessaire.", "Appliquer");
        if (!ok) return;

        // Fusionne avec le mapping déjà actif pour ne pas l'écraser.
        var combined = _service.GetCurrentMapping()
            .Where(existing => _pending.All(p => p.SourceCode != existing.SourceCode))
            .Concat(_pending)
            .ToList();

        if (_service.ApplyMapping(combined))
        {
            LogService.Instance.Log($"{_pending.Count} mapping(s) appliqué(s). Redémarrez pour activer.", LogLevel.Success);
            _pending.Clear();
            RenderPending();
            LoadCurrent();
        }
        else
        {
            LogService.Instance.Log("Échec de l'application du remapping.", LogLevel.Error);
        }
    }

    private void LoadCurrent()
    {
        var current = _service.GetCurrentMapping();
        CurrentList.Items.Clear();
        foreach (var m in current)
        {
            CurrentList.Items.Add(BuildMappingRow(m.SourceName, m.DestName, () =>
            {
                var remaining = current.Where(x => x.SourceCode != m.SourceCode).ToList();
                if (remaining.Count == 0) _service.RemoveMapping();
                else _service.ApplyMapping(remaining);
                LogService.Instance.Log($"Mapping « {m.SourceName} → {m.DestName} » retiré. Redémarrez pour appliquer.", LogLevel.Success);
                LoadCurrent();
            }));
        }
        CurrentEmptyLabel.Visibility = current.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshCurrent_Click(object sender, RoutedEventArgs e) => LoadCurrent();

    private void RemoveAll_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Ask(owner, "Supprimer tous les mappings", "Supprimer tous les remappings actifs ?", "Supprimer", danger: true)) return;

        if (_service.RemoveMapping())
            LogService.Instance.Log("Tous les remappings ont été supprimés. Redémarrez pour appliquer.", LogLevel.Success);
        else
            LogService.Instance.Log("Aucun remapping trouvé à supprimer.", LogLevel.Warning);
        LoadCurrent();
    }
}
