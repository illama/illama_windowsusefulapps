using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : contrôle de la "puissance" de la molette de souris.
///
/// Deux modes, au choix :
///  - "Tout l'ordinateur" : réglage Windows officiel (SystemParametersInfo + Panneau de configuration),
///    pris en compte immédiatement par toutes les applications, persistant nativement.
///  - "Application spécifique" : un multiplicateur appliqué UNIQUEMENT quand la fenêtre sous le curseur
///    appartient à l'exécutable (ou au dossier) ciblé, via un crochet souris bas niveau (WH_MOUSE_LL).
///    Toutes les autres applications ne sont jamais affectées.
///
/// Le crochet bas niveau tourne sur un thread dédié avec sa propre boucle de messages (obligatoire pour
/// que Windows puisse lui livrer les évènements). Chaque évènement de molette réellement intercepté est
/// bloqué puis réinjecté avec un delta multiplié via SendInput, marqué d'un repère (dwExtraInfo) pour que
/// le crochet reconnaisse et laisse passer sans le retraiter — indispensable pour éviter une boucle
/// infinie (l'évènement réinjecté repasse lui aussi par le crochet bas niveau).</summary>
public class WheelPowerService
{
    public static WheelPowerService Instance { get; } = new();

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnaireSystemePro");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "wheelpower.json");

    public WheelPowerSettings Current { get; private set; }

    public event Action<WheelBoostStat>? Boosted;

    private WheelPowerService()
    {
        Current = Load();
        if (Current.Mode == WheelPowerMode.SpecificApp && Current.AppModeEnabled && !string.IsNullOrWhiteSpace(Current.TargetPath))
        {
            try { StartHook(); }
            catch { /* pas bloquant au démarrage ; l'utilisateur peut réactiver depuis la page Molette */ }
        }
    }

    private static WheelPowerSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<WheelPowerSettings>(File.ReadAllText(SettingsPath)) ?? new WheelPowerSettings();
        }
        catch { /* fichier corrompu ou illisible : valeurs par défaut */ }
        return new WheelPowerSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* pas bloquant */ }
    }

    // ===================== Mode "Tout l'ordinateur" (API Windows officielle) =====================

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out uint pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    private const uint SPI_GETWHEELSCROLLLINES = 0x0068;
    private const uint SPI_SETWHEELSCROLLLINES = 0x0069;
    private const uint SPI_GETWHEELSCROLLCHARS = 0x006C;
    private const uint SPI_SETWHEELSCROLLCHARS = 0x006D;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public const int MinSteps = 1;
    public const int MaxSteps = 20; // même plage que le panneau "Souris" de Windows

    /// <summary>Nombre de lignes défilées par cran de molette (vertical). Windows par défaut : 3.</summary>
    public int GetVerticalLines() =>
        SystemParametersInfo(SPI_GETWHEELSCROLLLINES, 0, out uint value, 0) ? (int)value : ReadFallback("WheelScrollLines", 3);

    public void SetVerticalLines(int lines)
    {
        lines = Math.Clamp(lines, MinSteps, MaxSteps);
        SystemParametersInfo(SPI_SETWHEELSCROLLLINES, (uint)lines, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    /// <summary>Nombre de caractères défilés par cran (défilement horizontal, ex. Maj+molette). Défaut : 3.</summary>
    public int GetHorizontalChars() =>
        SystemParametersInfo(SPI_GETWHEELSCROLLCHARS, 0, out uint value, 0) ? (int)value : ReadFallback("WheelScrollChars", 3);

    public void SetHorizontalChars(int chars)
    {
        chars = Math.Clamp(chars, MinSteps, MaxSteps);
        SystemParametersInfo(SPI_SETWHEELSCROLLCHARS, (uint)chars, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    private static int ReadFallback(string valueName, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var val = key?.GetValue(valueName);
            return val != null ? Convert.ToInt32(val) : fallback;
        }
        catch { return fallback; }
    }

    public void ResetGlobalDefaults()
    {
        SetVerticalLines(3);
        SetHorizontalChars(3);
    }

    // ===================== Mode "Application spécifique" (crochet bas niveau) =====================

    public bool IsAppModeRunning { get; private set; }
    public long TotalBoostedEvents { get; private set; }

    public void SetMode(WheelPowerMode mode)
    {
        Current.Mode = mode;
        if (mode == WheelPowerMode.Global) StopHook(); // le mode "Application spécifique" doit être inactif hors de son propre mode
        Save();
    }

    public void SetTarget(string path, bool isFolder)
    {
        Current.TargetPath = path;
        Current.TargetIsFolder = isFolder;
        Save();
    }

    public void SetMultiplier(double multiplier)
    {
        Current.Multiplier = Math.Clamp(multiplier, 0.25, 8.0);
        Save();
    }

    /// <summary>Active/désactive l'interception pour l'application ciblée.</summary>
    public (bool Ok, string Message) SetAppModeEnabled(bool enabled)
    {
        if (enabled && string.IsNullOrWhiteSpace(Current.TargetPath))
            return (false, "Choisissez d'abord une application ou un dossier à cibler.");

        Current.AppModeEnabled = enabled;
        Save();

        try
        {
            if (enabled) StartHook(); else StopHook();
            return (true, enabled ? "Amplification activée pour l'application ciblée." : "Amplification désactivée.");
        }
        catch (Exception ex)
        {
            Current.AppModeEnabled = false;
            Save();
            return (false, "Échec : " + ex.Message);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_QUIT = 0x0012;
    private const uint GA_ROOT = 2;
    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    // Repère posé sur les évènements que l'on réinjecte nous-mêmes, pour que le crochet les reconnaisse
    // et les laisse passer sans les ré-amplifier (sinon : boucle infinie, l'évènement réinjecté repassant
    // lui aussi par le crochet bas niveau).
    private const uint InjectedMarker = 0x57474D50; // "WGMP"

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }

    private LowLevelMouseProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private Thread? _hookThread;
    private uint _hookThreadId;

    // Cache chemin de processus par PID : évite de résoudre MainModule.FileName (relativement coûteux)
    // à chaque cran de molette, ce qui pourrait ralentir le crochet au point que Windows le désactive.
    private readonly Dictionary<uint, (string Path, DateTime Expires)> _pathCache = new();

    private void StartHook()
    {
        if (IsAppModeRunning) return;
        _proc = HookCallback; // conservé en champ : sinon le délégué peut être ramassé par le GC

        var ready = new ManualResetEventSlim(false);
        _hookThread = new Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, IntPtr.Zero, 0);
            ready.Set();
            if (_hookId == IntPtr.Zero) return;

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        })
        { IsBackground = true, Name = "WheelPowerHook" };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        ready.Wait(2000);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException("Impossible d'installer le crochet souris (SetWindowsHookEx a échoué).");

        IsAppModeRunning = true;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private void StopHook()
    {
        if (!IsAppModeRunning) return;
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        _hookThread?.Join(1500);
        _hookThread = null;
        IsAppModeRunning = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // C'est un évènement qu'on a nous-mêmes réinjecté juste en dessous : on le laisse filer tel
            // quel, sans quoi il serait détecté à nouveau ici et ré-amplifié indéfiniment.
            if (data.dwExtraInfo.ToUInt64() == InjectedMarker)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            double multiplier = Current.Multiplier;
            if (Current.AppModeEnabled && Math.Abs(multiplier - 1.0) > 0.01 && MatchesTarget(data.pt))
            {
                short originalDelta = unchecked((short)(data.mouseData >> 16));
                int applied = (int)Math.Round(originalDelta * multiplier);
                applied = Math.Clamp(applied, short.MinValue, short.MaxValue);

                SendAmplifiedWheel(applied);
                TotalBoostedEvents++;
                Boosted?.Invoke(new WheelBoostStat(LastMatchedProcessName ?? "?", originalDelta, applied, TotalBoostedEvents));

                return (IntPtr)1; // bloque l'évènement d'origine : seul celui réinjecté doit atteindre l'appli
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private string? LastMatchedProcessName;

    private bool MatchesTarget(POINT pt)
    {
        if (string.IsNullOrWhiteSpace(Current.TargetPath)) return false;

        IntPtr hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return false;
        hwnd = GetAncestor(hwnd, GA_ROOT);
        if (hwnd == IntPtr.Zero) return false;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;

        string? path = ResolveProcessPath(pid);
        if (path == null) return false;

        LastMatchedProcessName = Path.GetFileName(path);

        return Current.TargetIsFolder
            ? path.StartsWith(Current.TargetPath, StringComparison.OrdinalIgnoreCase)
            : path.Equals(Current.TargetPath, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveProcessPath(uint pid)
    {
        if (_pathCache.TryGetValue(pid, out var cached) && cached.Expires > DateTime.UtcNow)
            return cached.Path;

        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            string path = proc.MainModule?.FileName ?? "";
            _pathCache[pid] = (path, DateTime.UtcNow.AddSeconds(5));
            return path;
        }
        catch
        {
            _pathCache[pid] = ("", DateTime.UtcNow.AddSeconds(5));
            return null;
        }
    }

    private static void SendAmplifiedWheel(int delta)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = 0, dy = 0,
                mouseData = unchecked((uint)delta),
                dwFlags = MOUSEEVENTF_WHEEL,
                time = 0,
                dwExtraInfo = (UIntPtr)InjectedMarker,
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}
