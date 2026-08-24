using Microsoft.Win32;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Remapping clavier bas niveau via la clé de registre "Scancode Map"
/// (Keyboard Layout), identique au mécanisme utilisé par le script PowerShell d'origine.</summary>
public class KeyboardRemapService
{
    private const string RegPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";

    public static readonly KeyOption[] AllKeys =
    {
        new("A",0x1E), new("B",0x30), new("C",0x2E), new("D",0x20), new("E",0x12), new("F",0x21), new("G",0x22), new("H",0x23),
        new("I",0x17), new("J",0x24), new("K",0x25), new("L",0x26), new("M",0x32), new("N",0x31), new("O",0x18), new("P",0x19),
        new("Q",0x10), new("R",0x13), new("S",0x1F), new("T",0x14), new("U",0x16), new("V",0x2F), new("W",0x11), new("X",0x2D),
        new("Y",0x15), new("Z",0x2C),
        new("1",0x02), new("2",0x03), new("3",0x04), new("4",0x05), new("5",0x06), new("6",0x07), new("7",0x08), new("8",0x09), new("9",0x0A), new("0",0x0B),
        new("ESC",0x01), new("TAB",0x0F), new("CAPSLOCK",0x3A),
        new("SHIFT (gauche)",0x2A), new("SHIFT (droite)",0x36),
        new("CTRL (gauche)",0x1D), new("ALT (gauche)",0x38),
        new("SPACE",0x39), new("ENTER",0x1C), new("BACKSPACE",0x0E),
        new("DELETE",0xE053), new("INSERT",0xE052),
        new("F1",0x3B), new("F2",0x3C), new("F3",0x3D), new("F4",0x3E), new("F5",0x3F), new("F6",0x40),
        new("F7",0x41), new("F8",0x42), new("F9",0x43), new("F10",0x44), new("F11",0x57), new("F12",0x58),
        new("MINUS -",0x0C), new("EQUALS =",0x0D),
        new("[",0x1A), new("]",0x1B), new(";",0x27), new("'",0x28), new("\\",0x2B),
        new(",",0x33), new(".",0x34), new("/",0x35), new("`",0x29),
        new("HAUT",0xE048), new("BAS",0xE050), new("GAUCHE",0xE04B), new("DROITE",0xE04D),
        new("PAGE PREC.",0xE049), new("PAGE SUIV.",0xE051), new("HOME",0xE047), new("END",0xE04F),
        new("NUMLOCK",0x45),
        new("PRINTSCREEN",0x54), new("SCROLLLOCK",0x46),
        new("WIN (gauche)",0xE05B), new("WIN (droite)",0xE05C), new("MENU",0xE05D),
        new("[ DÉSACTIVER LA TOUCHE ]",0x00),
    };

    public static string NameFor(ushort code) =>
        AllKeys.FirstOrDefault(k => k.Code == code)?.Name ?? $"0x{code:X}";

    public bool ApplyMapping(IEnumerable<KeyMapping> mappings)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegPath);
        var list = mappings.ToList();

        var bytes = new List<byte>();
        bytes.AddRange(new byte[8]); // en-tête réservé
        bytes.AddRange(BitConverter.GetBytes((uint)(list.Count + 1)));
        foreach (var m in list)
        {
            bytes.AddRange(BitConverter.GetBytes(m.DestCode));
            bytes.AddRange(BitConverter.GetBytes(m.SourceCode));
        }
        bytes.AddRange(new byte[4]); // terminateur nul

        try
        {
            key.SetValue("Scancode Map", bytes.ToArray(), RegistryValueKind.Binary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RemoveMapping()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegPath, writable: true);
        if (key?.GetValue("Scancode Map") == null) return false;
        key.DeleteValue("Scancode Map", throwOnMissingValue: false);
        return true;
    }

    public List<KeyMapping> GetCurrentMapping()
    {
        var result = new List<KeyMapping>();
        using var key = Registry.LocalMachine.OpenSubKey(RegPath);
        if (key?.GetValue("Scancode Map") is not byte[] bytes || bytes.Length < 12) return result;

        uint numEntries = BitConverter.ToUInt32(bytes, 8);
        if (numEntries <= 1) return result;

        for (int i = 0; i < numEntries - 1; i++)
        {
            int offset = 12 + i * 4;
            if (offset + 4 > bytes.Length) break;
            ushort dest = BitConverter.ToUInt16(bytes, offset);
            ushort source = BitConverter.ToUInt16(bytes, offset + 2);
            result.Add(new KeyMapping
            {
                SourceCode = source,
                DestCode = dest,
                SourceName = NameFor(source),
                DestName = dest == 0 ? "DÉSACTIVÉE" : NameFor(dest),
            });
        }
        return result;
    }
}
