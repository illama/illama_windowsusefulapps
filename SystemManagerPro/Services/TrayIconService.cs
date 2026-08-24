namespace SystemManagerPro.Services;

/// <summary>Icône dans la zone de notification (barre d'état système) — encapsule
/// System.Windows.Forms.NotifyIcon, seule API disponible pour ça en WPF.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIconService(string tooltip)
    {
        // Réutilise l'icône déjà embarquée dans l'exécutable (ApplicationIcon du .csproj)
        // plutôt que de recharger un fichier séparé — toujours cohérent avec la barre des tâches.
        System.Drawing.Icon icon;
        try
        {
            string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            icon = System.Drawing.SystemIcons.Application;
        }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = tooltip,
            Visible = false,
            ContextMenuStrip = menu,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) OpenRequested?.Invoke();
        };
    }

    public void Show() => _notifyIcon.Visible = true;
    public void Hide() => _notifyIcon.Visible = false;

    public void ShowBalloon(string title, string text) =>
        _notifyIcon.ShowBalloonTip(3000, title, text, System.Windows.Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
