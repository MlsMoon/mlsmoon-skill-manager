using System.Text;
using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class CatalogMetaSync
{
    private readonly AppPaths _paths;
    private readonly GhCli _gh;
    private readonly GitRemote _git;
    private readonly SkillGit _skillGit;
    private readonly IProcessRunner _runner;
    private readonly CatalogMetaCache _cache;
    private readonly Func<string> _nasUser;
    private readonly Func<string> _workspace;

    public CatalogMetaSync(
        AppPaths paths,
        GhCli gh,
        GitRemote git,
        SkillGit skillGit,
        Func<string> nasUser,
        Func<string> workspace,
        IProcessRunner? runner = null)
    {
        _paths = paths;
        _gh = gh;
        _git = git;
        _skillGit = skillGit;
        _runner = runner ?? new ProcessRunner();
        _cache = new CatalogMetaCache(paths);
        _nasUser = nasUser;
        _workspace = workspace;
    }

    public void ApplyCached(IEnumerable<SkillDefinition> skills, Action<SkillDefinition, CatalogMetaEntry> apply)
    {
        foreach (var skill in skills)
        {
            var cached = _cache.Find(skill.Id);
            if (cached is not null)
            {
                apply(skill, cached);
            }
        }
    }

    public async Task<CatalogMetaEntry?> EnsureAsync(
        SkillDefinition skill,
        string? commit,
        string? branch,
        bool canReach,
        CancellationToken cancellationToken = default)
    {
        var file = CatalogMeta.FilePath(skill);
        var cached = _cache.Find(skill.Id);
        if (cached is not null && _cache.Matches(cached, skill, file, commit))
        {
            return cached;
        }

        if (TryReadLocal(skill, file, commit, branch) is { } local)
        {
            _cache.Upsert(local);
            return local;
        }

        if (!canReach)
        {
            return cached;
        }

        commit = string.IsNullOrWhiteSpace(commit)
            ? await ResolveCommitAsync(skill, branch, cancellationToken).ConfigureAwait(false)
            : commit;
        cached = _cache.Find(skill.Id);
        if (cached is not null && _cache.Matches(cached, skill, file, commit))
        {
            return cached;
        }

        var text = await FetchAsync(skill, file, branch, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return cached;
        }

        var parsed = CatalogMeta.Parse(text);
        var entry = new CatalogMetaEntry
        {
            Id = skill.Id,
            Repo = string.IsNullOrWhiteSpace(skill.Repo) ? skill.Host + skill.GitPath : skill.Repo,
            File = file,
            Commit = commit ?? "",
            Name = string.IsNullOrWhiteSpace(parsed.Name) ? skill.Id : parsed.Name,
            Description = parsed.Description
        };
        _cache.Upsert(entry);
        return entry;
    }

    public static void Apply(SkillDefinition skill, CatalogMetaEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            skill.Name = entry.Name;
        }

        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            skill.Description = entry.Description;
        }

        if (!skill.IsProjectCopy)
        {
            return;
        }

        foreach (var companion in skill.CompanionSkills)
        {
            companion.ParentPluginName = skill.DisplayName;
        }
    }

    private CatalogMetaEntry? TryReadLocal(
        SkillDefinition skill,
        string file,
        string? commit,
        string? branch)
    {
        var directory = LocalRoot(skill, branch);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        foreach (var candidate in CatalogMeta.LocalCandidates(file))
        {
            var path = Path.Combine(directory, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            var parsed = CatalogMeta.Parse(File.ReadAllText(path));
            if (string.IsNullOrWhiteSpace(parsed.Name) && string.IsNullOrWhiteSpace(parsed.Description))
            {
                continue;
            }

            return new CatalogMetaEntry
            {
                Id = skill.Id,
                Repo = string.IsNullOrWhiteSpace(skill.Repo) ? skill.Host + skill.GitPath : skill.Repo,
                File = file,
                Commit = commit ?? "",
                Name = string.IsNullOrWhiteSpace(parsed.Name) ? skill.Id : parsed.Name,
                Description = parsed.Description
            };
        }

        return null;
    }

    private string? LocalRoot(SkillDefinition skill, string? branch)
    {
        if (skill.IsLan)
        {
            var name = string.IsNullOrWhiteSpace(branch) ? skill.Branches.FirstOrDefault()?.Name : branch;
            return string.IsNullOrWhiteSpace(name)
                ? null
                : _paths.LanCacheDirectory(skill.Host, skill.ResolvedInstallName, name);
        }

        return RepoUrl.TryParse(skill.Repo, out var repo) ? _paths.RepoCacheDirectory(repo) : null;
    }

    private async Task<string> ResolveCommitAsync(
        SkillDefinition skill,
        string? branch,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(branch))
        {
            var known = await _skillGit.RemoteCommitAsync(skill, branch, _nasUser(), cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(known))
            {
                return known;
            }
        }

        if (skill.IsLan || !RepoUrl.TryParse(skill.Repo, out var repo))
        {
            return "";
        }

        return await _gh.BranchCommitAsync(repo, "HEAD", cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> FetchAsync(
        SkillDefinition skill,
        string file,
        string? branch,
        CancellationToken cancellationToken)
    {
        if (skill.IsLan)
        {
            var previous = skill.ReadmePath;
            skill.ReadmePath = file;
            try
            {
                var name = string.IsNullOrWhiteSpace(branch)
                    ? UnityWorkspace.PickBranch(skill, UnityWorkspace.ReadEditorVersion(_workspace()))?.Name
                    : branch;
                return await _git.TryReadReadmeAsync(
                        skill,
                        _nasUser(),
                        string.IsNullOrWhiteSpace(name) ? "HEAD" : name,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                skill.ReadmePath = previous;
            }
        }

        if (!RepoUrl.TryParse(skill.Repo, out var repo))
        {
            return "";
        }

        var text = await ReadGitHubAsync(repo, file, cancellationToken).ConfigureAwait(false);
        if (text.Length > 0 || CatalogMeta.IsReadmeName(file))
        {
            return text;
        }

        return await ReadGitHubAsync(repo, "README.md", cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ReadGitHubAsync(
        GitHubRepoRef repo,
        string file,
        CancellationToken cancellationToken)
    {
        var api = CatalogMeta.IsReadmeName(file) && !file.Contains('/', StringComparison.Ordinal)
            ? $"repos/{repo.OwnerRepo}/readme"
            : $"repos/{repo.OwnerRepo}/contents/{file.Replace('\\', '/').TrimStart('/')}";
        var result = await _runner.RunAsync("gh", ["api", api], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? DecodeContent(result.StdOut) : "";
    }

    private static string DecodeContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("content", out var content))
            {
                return "";
            }

            var raw = (content.GetString() ?? "").Replace("\n", "", StringComparison.Ordinal);
            return raw.Length == 0 ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        }
        catch (FormatException)
        {
            return "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
