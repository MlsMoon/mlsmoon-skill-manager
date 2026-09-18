namespace MlsmoonSkillManager.Core.Services;

public static class SkillCopy
{
    public const string UserGitCopyMessage = "带有 .git，不能整目录覆盖。请用安装目录里的 git 快进。";

    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea"
    };

    public static bool HasGitRepo(string directory) =>
        !string.IsNullOrWhiteSpace(directory) && Directory.Exists(Path.Combine(directory, ".git"));

    public static void Replace(string source, string dest, ISet<string>? extraSkip = null, bool includeGit = false)
    {
        if (Directory.Exists(dest))
        {
            if (HasGitRepo(dest))
            {
                throw new InvalidOperationException(dest + " " + UserGitCopyMessage);
            }

            Directory.Delete(dest, true);
        }

        CopyDirectory(source, dest, extraSkip, includeGit);
    }

    private static void CopyDirectory(string source, string dest, ISet<string>? extraSkip, bool includeGit)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkip(name, extraSkip, includeGit))
            {
                continue;
            }

            File.Copy(file, Path.Combine(dest, name), true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (ShouldSkip(name, extraSkip, includeGit))
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(dest, name), extraSkip, includeGit);
        }
    }

    private static bool ShouldSkip(string name, ISet<string>? extraSkip, bool includeGit)
    {
        if (includeGit && name.Equals(".git", StringComparison.OrdinalIgnoreCase))
        {
            return extraSkip is not null && extraSkip.Contains(name);
        }

        return SkipNames.Contains(name) || extraSkip is not null && extraSkip.Contains(name);
    }
}
