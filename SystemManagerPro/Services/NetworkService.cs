using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SystemManagerPro.Services;

public record AdapterInfo(
    string Nom, string Description, string Statut, string TypePhysique,
    string Ipv4, string Passerelle, string Dns, string MacAddress, long VitesseMbps);

public record PingResultLine(int Sequence, string Cible, long Ms, string Statut);

/// <summary>Nouvelle fonctionnalité : diagnostic réseau (adaptateurs, DNS, ping) et outils de dépannage courants.</summary>
public class NetworkService
{
    public List<AdapterInfo> GetAdapters()
    {
        var result = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = nic.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "—";
            var gateway = props.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "—";
            var dns = string.Join(", ", props.DnsAddresses.Select(a => a.ToString()));
            var mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

            result.Add(new AdapterInfo(
                nic.Name,
                nic.Description,
                nic.OperationalStatus.ToString(),
                nic.NetworkInterfaceType.ToString(),
                ipv4, gateway,
                string.IsNullOrWhiteSpace(dns) ? "—" : dns,
                string.IsNullOrWhiteSpace(mac) ? "—" : mac,
                nic.Speed > 0 ? nic.Speed / 1_000_000 : 0));
        }
        return result.OrderByDescending(a => a.Statut == "Up").ToList();
    }

    public ProcessRunner.RunResult FlushDns() => ProcessRunner.Run("ipconfig", "/flushdns");

    public ProcessRunner.RunResult ResetWinsock() => ProcessRunner.Run("netsh", "winsock reset");

    public ProcessRunner.RunResult ResetTcpIp() => ProcessRunner.Run("netsh", "int ip reset");

    public ProcessRunner.RunResult ReleaseRenew()
    {
        var release = ProcessRunner.Run("ipconfig", "/release");
        var renew = ProcessRunner.Run("ipconfig", "/renew");
        return new ProcessRunner.RunResult(renew.ExitCode, release.StdOut + Environment.NewLine + renew.StdOut, renew.StdErr);
    }

    public async Task<List<PingResultLine>> PingAsync(string host, int count = 4)
    {
        var results = new List<PingResultLine>();
        using var ping = new Ping();
        for (int i = 1; i <= count; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 2000);
                results.Add(new PingResultLine(i, host, reply.RoundtripTime,
                    reply.Status == IPStatus.Success ? "OK" : reply.Status.ToString()));
            }
            catch (Exception ex)
            {
                results.Add(new PingResultLine(i, host, -1, ex.Message));
            }
        }
        return results;
    }
}
