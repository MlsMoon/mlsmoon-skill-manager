using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SnapshotFile
{
    public string Commit { get; set; } = "";
    public string Branch { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class InstallSnapshot
{
    public static readonly HashSet<string> IgnoreNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea",
        SkillInstaller.MarkerFileName, SkillInstaller.PluginMarkerFileName, SkillInstaller.PackageMarkerFileName,
        "Thumbs.db", ".DS_Store"
    };

    public static void Write(string snapshotPath, string directory, string commit, string branch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        var file = new SnapshotFile
        {
            Commit = commit,
            Branch = branch,
            Files = HashTree(directory)
        };
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(file, JsonUtil.Options));
    }

    public static void Delete(string snapshotPath)
    {
        if (File.Exists(snapshotPath))
        {
            File.Delete(snapshotPath);
        }
    }

    public static SnapshotFile? Read(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SnapshotFile>(File.ReadAllText(snapshotPath), JsonUtil.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<GitChange> Diff(string snapshotPath, string directory)
    {
        var snap = Read(snapshotPath);
        if (snap is null)
        {
            return [];
        }

        return Diff(snap.Files, HashTree(directory));
    }

    public static IReadOnlyList<GitChange> Diff(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var changes = new List<GitChange>();
        foreach (var path in expected.Keys.Union(actual.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var hasExpected = expected.TryGetValue(path, out var left);
            var hasActual = actual.TryGetValue(path, out var right);
            if (hasExpected && hasActual)
            {
                if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(new GitChange { Kind = GitChangeKind.Modified, Path = path });
                }

                continue;
            }

            changes.Add(new GitChange
            {
                Kind = hasActual ? GitChangeKind.Added : GitChangeKind.Deleted,
                Path = path
            });
        }

        return changes;
    }

    public static Dictionary<string, string> HashTree(string directory)
    {
        var map = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var files = new List<string>();
        CollectFiles(root, root, files);
        Parallel.ForEach(files, file =>
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            map[relative] = HashFile(file);
        });
        return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
    }

    private static void CollectFiles(string root, string current, List<string> files)
    {
        foreach (var file in Directory.GetFiles(current))
        {
            if (!IgnoreNames.Contains(Path.GetFileName(file)))
            {
                files.Add(file);
            }
        }

        foreach (var dir in Directory.GetDirectories(current))
        {
            if (IgnoreNames.Contains(Path.GetFileName(dir)))
            {
                continue;
            }

            CollectFiles(root, dir, files);
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
