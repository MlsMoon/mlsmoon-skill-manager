namespace MlsmoonSkillManager.Core.Services;

public static class SkillCopy
{
    public const string UserGitCopyMessage = "带有 .git，是用户自己的工作副本。本工具不会覆盖；请在该目录里自行更新。";

    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea"
    };

    public static bool HasGitRepo(string directory) =>
        !string.IsNullOrWhiteSpace(directory) && Directory.Exists(Path.Combine(directory, ".git"));

    public static void ThrowIfUserGitCopy(string dest)
    {
        if (HasGitRepo(dest))
        {
            throw new InvalidOperationException(dest + " " + UserGitCopyMessage);
        }
    }

    public static void Replace(string source, string dest, ISet<string>? extraSkip = null)
    {
        if (Directory.Exists(dest))
        {
            ThrowIfUserGitCopy(dest);
            Directory.Delete(dest, true);
        }

        CopyDirectory(source, dest, extraSkip);
    }

    private static void CopyDirectory(string source, string dest, ISet<string>? extraSkip)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkip(name, extraSkip))
            {
                continue;
            }

            File.Copy(file, Path.Combine(dest, name), true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (ShouldSkip(name, extraSkip))
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(dest, name), extraSkip);
        }
    }

    private static bool ShouldSkip(string name, ISet<string>? extraSkip)
    {
        return SkipNames.Contains(name) || extraSkip is not null && extraSkip.Contains(name);
    }
}
