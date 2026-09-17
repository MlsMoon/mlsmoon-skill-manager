using System.Net.Http.Headers;
using System.Text.Json;

namespace MlsmoonSkillManager.Core.Services;

public readonly record struct DownloadProgress(long Received, long? Total)
{
    public double Percent => Total is > 0 ? 100.0 * Received / Total.Value : 0;
    public bool HasTotal => Total is > 0;
}

public sealed class AppReleaseInfo
{
    public string Tag { get; init; } = "";
    public Version Version { get; init; } = new(0, 0, 0);
    public string HtmlUrl { get; init; } = "";
    public string SetupUrl { get; init; } = "";
    public string SetupName { get; init; } = "";
    public string Notes { get; init; } = "";
    public bool HasSetup => !string.IsNullOrWhiteSpace(SetupUrl);
}

public sealed class UpdateService
{
    public const string OwnerRepo = "MlsMoon/moon-game-dev-tool-manager";
    public const string ReleasesUrl = "https://github.com/" + OwnerRepo + "/releases";
    public const string LatestApiUrl = "https://api.github.com/repos/" + OwnerRepo + "/releases/latest";
    public const string SilentSetupArgs =
        "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS";

    private static readonly HttpClient Http = CreateClient();

    public async Task<AppReleaseInfo> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(LatestApiUrl, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"读取 GitHub Release 失败 ({(int)response.StatusCode})。");
        }

        return Parse(body);
    }

    public async Task DownloadAsync(
        string url,
        string destination,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        var lastReport = DateTime.MinValue;
        progress?.Report(new DownloadProgress(0, total));
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            var now = DateTime.UtcNow;
            if (progress is not null && (now - lastReport >= TimeSpan.FromMilliseconds(80) || received == total))
            {
                lastReport = now;
                progress.Report(new DownloadProgress(received, total));
            }
        }

        progress?.Report(new DownloadProgress(received, total ?? received));
    }

    public static bool IsNewer(Version latest, Version current)
    {
        return Normalize(latest) > Normalize(current);
    }

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return Version.TryParse(text, out version!);
    }

    public static AppReleaseInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
        if (!TryParseTag(tag, out var version))
        {
            throw new InvalidOperationException($"Release 标签无法识别: {tag}");
        }

        var html = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() ?? "" : ReleasesUrl;
        var notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
        var setupUrl = "";
        var setupName = "";
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                var url = asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
                if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(url))
                {
                    setupName = name;
                    setupUrl = url;
                    break;
                }
            }
        }

        return new AppReleaseInfo
        {
            Tag = tag,
            Version = version,
            HtmlUrl = html,
            SetupUrl = setupUrl,
            SetupName = setupName,
            Notes = notes.Trim()
        };
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            version.Build < 0 ? 0 : version.Build);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MoonGameDevToolManager", "1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
