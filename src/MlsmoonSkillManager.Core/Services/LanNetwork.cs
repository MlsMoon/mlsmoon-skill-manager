using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MlsmoonSkillManager.Core.Services;

public sealed class LanProbe
{
    public bool OnLan { get; init; }
    public bool HostReachable { get; init; }
    public string LocalAddress { get; init; } = "";
    public string Detail { get; init; } = "";
}

public static class LanNetwork
{
    public const int SshPort = 22;

    public static bool IsSameSubnet(IPAddress local, IPAddress host)
    {
        if (local.AddressFamily != AddressFamily.InterNetwork
            || host.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var left = local.GetAddressBytes();
        var right = host.GetAddressBytes();
        return left[0] == right[0] && left[1] == right[1] && left[2] == right[2];
    }

    public static string? LocalAddressOnSubnet(IPAddress host)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var info in nic.GetIPProperties().UnicastAddresses)
            {
                if (IsSameSubnet(info.Address, host))
                {
                    return info.Address.ToString();
                }
            }
        }

        return null;
    }

    public static async Task<bool> CanReachAsync(
        string host,
        int port = SshPort,
        int timeoutMs = 800,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<LanProbe> ProbeAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new LanProbe
            {
                OnLan = false,
                Detail = "未配置局域网主机。局域网包只写在本机 override，不进公开 catalog。"
            };
        }

        var target = host.Trim();
        var local = IPAddress.TryParse(target, out var hostIp)
            ? LocalAddressOnSubnet(hostIp)
            : null;
        var reachable = await CanReachAsync(target, SshPort, 800, cancellationToken).ConfigureAwait(false);
        var onLan = local is not null || reachable;
        if (local is not null && reachable)
        {
            return new LanProbe
            {
                OnLan = true,
                HostReachable = true,
                LocalAddress = local,
                Detail = $"局域网：本机 {local}，主机可达"
            };
        }

        if (local is not null)
        {
            return new LanProbe
            {
                OnLan = true,
                HostReachable = false,
                LocalAddress = local,
                Detail = $"局域网：本机 {local}，但主机端口 {SshPort} 不通"
            };
        }

        if (reachable)
        {
            return new LanProbe
            {
                OnLan = true,
                HostReachable = true,
                Detail = "局域网：主机可达"
            };
        }

        return new LanProbe
        {
            OnLan = false,
            Detail = "不在该局域网，主机不可达"
        };
    }
}
