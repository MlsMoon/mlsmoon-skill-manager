using System.Text.RegularExpressions;

namespace MlsmoonSkillManager.Core.Services;

public sealed record GitHubRepoRef(string Owner, string Name)
{
    public string OwnerRepo => $"{Owner}/{Name}";
    public string HttpsUrl => $"https://github.com/{Owner}/{Name}";
}

public static class RepoUrl
{
    private static readonly Regex OwnerRepoRegex = new(
        @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled);

    public static bool TryParse(string? value, out GitHubRepoRef repo)
    {
        repo = new GitHubRepoRef("", "");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        if (text.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["git@github.com:".Length..];
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
                 && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            text = uri.AbsolutePath.Trim('/');
        }

        var slash = text.IndexOf('/');
        if (slash <= 0 || slash != text.LastIndexOf('/'))
        {
            return false;
        }

        var owner = text[..slash];
        var name = text[(slash + 1)..];
        if (!OwnerRepoRegex.IsMatch($"{owner}/{name}"))
        {
            return false;
        }

        repo = new GitHubRepoRef(owner, name);
        return true;
    }

    public static GitHubRepoRef Parse(string value)
    {
        if (!TryParse(value, out var repo))
        {
            throw new FormatException($"不是有效的 GitHub 仓库地址: {value}");
        }

        return repo;
    }
}
