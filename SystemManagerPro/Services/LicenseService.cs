using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SystemManagerPro.Models;

namespace SystemManagerPro.Services;

/// <summary>Nouvelle fonctionnalité : compte administrateur (déblocage total + génération de licences)
/// et vérification de licence pour les fonctionnalités payantes.
///
/// ⚠️ Limite assumée : cette application n'a pas de serveur. La signature HMAC empêche de FABRIQUER
/// une clé sans connaître le secret embarqué, mais elle ne peut pas empêcher la RÉUTILISATION d'une
/// clé valide sur plusieurs machines (ça demanderait un serveur central qui compte les activations).
/// Le nombre de PC autorisés est donc informatif (pour le suivi côté vendeur), pas techniquement imposé.</summary>
public class LicenseService
{
    public static LicenseService Instance { get; } = new();

    private const string AdminUsername = "maxence";
    // SHA-256("SGMP_salt_" + mot de passe) — évite que le mot de passe apparaisse en clair dans le binaire.
    private const string AdminPasswordHash = "C489D05A135A6C1694AD0B601E8C463A8C5F16A3D5FF4520719C48EE0D41B906";

    private static readonly byte[] Secret = Convert.FromBase64String("RKxP8W3xPfzK6Nj90Z3oWfmrKImleNlB8aTBWJKXhrY=");

    public static readonly HashSet<string> PaidFeatureKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "services", "processes", "language", "keyboard", "installer", "cleanup", "network",
        "uninstaller", "advanced", "tweaks",
    };

    public bool IsAdminSession { get; private set; }

    private static readonly string LicenseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnaireSystemePro");
    private static readonly string LicensePath = Path.Combine(LicenseDir, "license.dat");
    private static readonly string LedgerPath = Path.Combine(LicenseDir, "licenses_issued.json");

    private LicenseInfo? _cachedLicense;
    private bool _licenseChecked;

    // ===================== Session admin =====================

    public bool TryAdminLogin(string username, string password)
    {
        if (!string.Equals(username.Trim(), AdminUsername, StringComparison.OrdinalIgnoreCase)) return false;
        if (Hash(password) != AdminPasswordHash) return false;
        IsAdminSession = true;
        return true;
    }

    public void AdminLogout() => IsAdminSession = false;

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("SGMP_salt_" + input));
        return Convert.ToHexString(bytes);
    }

    // ===================== Génération (admin) =====================

    public string GenerateLicense(string customerName, int maxPcs, DateTime? expiry)
    {
        long expiryTicks = expiry?.Ticks ?? -1;
        string payload = $"{customerName}|{maxPcs}|{expiryTicks}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        string payloadB64 = ToUrlSafeBase64(payloadBytes);

        var sig = HMACSHA256.HashData(Secret, payloadBytes)[..8];
        string key = $"{payloadB64}.{Convert.ToHexString(sig)}";

        AppendToLedger(new IssuedLicenseRecord(customerName, maxPcs, expiry, key, DateTime.Now));
        return key;
    }

    public List<IssuedLicenseRecord> GetLedger()
    {
        try
        {
            if (!File.Exists(LedgerPath)) return new();
            return JsonSerializer.Deserialize<List<IssuedLicenseRecord>>(File.ReadAllText(LedgerPath)) ?? new();
        }
        catch { return new(); }
    }

    private void AppendToLedger(IssuedLicenseRecord record)
    {
        try
        {
            Directory.CreateDirectory(LicenseDir);
            var list = GetLedger();
            list.Insert(0, record);
            File.WriteAllText(LedgerPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* la génération reste utilisable même si l'écriture du registre local échoue */ }
    }

    // ===================== Vérification / activation =====================

    public LicenseInfo? ParseAndVerify(string key)
    {
        try
        {
            var parts = key.Trim().Split('.');
            if (parts.Length != 2) return null;

            var payloadBytes = FromUrlSafeBase64(parts[0]);
            var expectedSig = Convert.ToHexString(HMACSHA256.HashData(Secret, payloadBytes));
            var providedSig = parts[1].ToUpperInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expectedSig[..providedSig.Length]), Encoding.ASCII.GetBytes(providedSig)))
                return null;

            var fields = Encoding.UTF8.GetString(payloadBytes).Split('|');
            if (fields.Length != 3) return null;

            string customerName = fields[0];
            int maxPcs = int.Parse(fields[1]);
            long expiryTicks = long.Parse(fields[2]);
            DateTime? expiry = expiryTicks < 0 ? null : new DateTime(expiryTicks);

            return new LicenseInfo(customerName, maxPcs, expiry, key);
        }
        catch { return null; }
    }

    public (bool Ok, string Message) Activate(string key)
    {
        var info = ParseAndVerify(key);
        if (info == null) return (false, "Clé de licence invalide.");
        if (info.Expiry is { } exp && exp < DateTime.Now) return (false, "Cette licence a expiré.");

        try
        {
            Directory.CreateDirectory(LicenseDir);
            File.WriteAllText(LicensePath, key);
            _cachedLicense = info;
            _licenseChecked = true;
            return (true, $"Licence activée pour « {info.CustomerName} », valable jusqu'au {info.ExpiryLabel}.");
        }
        catch (Exception ex)
        {
            return (false, "Impossible d'enregistrer la licence : " + ex.Message);
        }
    }

    public void Deactivate()
    {
        try { if (File.Exists(LicensePath)) File.Delete(LicensePath); } catch { /* ignore */ }
        _cachedLicense = null;
        _licenseChecked = true;
    }

    public LicenseInfo? GetCurrentLicense()
    {
        if (_licenseChecked) return _cachedLicense;
        _licenseChecked = true;
        try
        {
            if (!File.Exists(LicensePath)) return _cachedLicense = null;
            var info = ParseAndVerify(File.ReadAllText(LicensePath).Trim());
            if (info?.Expiry is { } exp && exp < DateTime.Now) info = null;
            return _cachedLicense = info;
        }
        catch { return _cachedLicense = null; }
    }

    public bool IsFeatureUnlocked(string navKey) =>
        IsAdminSession || !PaidFeatureKeys.Contains(navKey) || GetCurrentLicense() != null;

    private static string ToUrlSafeBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromUrlSafeBase64(string s)
    {
        string b64 = s.Replace('-', '+').Replace('_', '/');
        switch (b64.Length % 4)
        {
            case 2: b64 += "=="; break;
            case 3: b64 += "="; break;
        }
        return Convert.FromBase64String(b64);
    }
}
