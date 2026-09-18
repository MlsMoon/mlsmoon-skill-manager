using System.Security.Cryptography;
using System.Text;

namespace MlsmoonSkillManager.Core.Services;

public sealed class AppPaths
{
    public AppPaths(string? appDirectory = null, string? configDirectory = null, string? cacheDirectory = null)
    {
        AppDirectory = appDirectory ?? AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        ConfigDirectory = configDirectory
            ?? ReadEnv("MLSMOON_CONFIG_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MlsmoonSkillManager");
        CacheDirectory = cacheDirectory
            ?? ReadEnv("MLSMOON_CACHE_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MlsmoonSkillManager",
                "cache");
        SettingsPath = Path.Combine(ConfigDirectory, "settings.json");
        UserOverridePath = Path.Combine(ConfigDirectory, "skills.override.json");
        BundledCatalogPath = Path.Combine(AppDirectory, "catalog", "skills.json");
        BundledOverridePath = Path.Combine(AppDirectory, "catalog", "skills.override.json");
        RepoOverridePath = FindRepoOverridePath(AppDirectory)
            ?? FindRepoOverridePath(Directory.GetCurrentDirectory());
    }

    public string AppDirectory { get; }
    public string ConfigDirectory { get; }
    public string CacheDirectory { get; }
    public string SettingsPath { get; }
    public string UserOverridePath { get; }
    public string BundledCatalogPath { get; }
    public string BundledOverridePath { get; }
    public string? RepoOverridePath { get; }

    public string EditableOverridePath => RepoOverridePath ?? UserOverridePath;

    private static string? FindRepoOverridePath(string start)
    {
        var dir = new DirectoryInfo(start);
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var catalog = Path.Combine(dir.FullName, "catalog", "skills.json");
            if (File.Exists(catalog))
            {
                return Path.Combine(dir.FullName, "catalog", "skills.override.json");
            }

            dir = dir.Parent;
        }

        return null;
    }

    public string SnapshotDirectory => Path.Combine(ConfigDirectory, "snapshots");
    public string UpdatesDirectory => Path.Combine(CacheDirectory, "updates");

    public string RepoCacheDirectory(GitHubRepoRef repo)
    {
        return NamedCache($"{repo.Owner}__{repo.Name}");
    }

    public string LanCacheDirectory(string host, string name, string branch)
    {
        return NamedCache($"lan__{host}__{name}__{branch}");
    }

    private string NamedCache(string safe)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return Path.Combine(CacheDirectory, safe);
    }

    public string SnapshotPath(string workspacePath, string skillId, string root)
    {
        var raw = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                  + "|" + skillId + "|" + root;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return Path.Combine(SnapshotDirectory, hash + ".json");
    }

    public void EnsureWritable()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(SnapshotDirectory);
    }

    private static string? ReadEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
