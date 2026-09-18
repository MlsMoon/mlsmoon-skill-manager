using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class WorkspaceGit
{
    private readonly IProcessRunner _runner;

    public WorkspaceGit(IProcessRunner runner)
    {
        _runner = runner;
    }

    public static bool HasRepo(string directory) => SkillCopy.HasGitRepo(directory);

    public Task<bool> HasHeadAsync(string directory, CancellationToken cancellationToken = default) =>
        HasRefAsync(directory, "HEAD", cancellationToken);

    public async Task<bool> HasRefAsync(
        string directory,
        string spec,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spec) || !HasRepo(directory))
        {
            return false;
        }

        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "rev-parse", "--verify", spec],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success;
    }

    public async Task<string> ReadHeadCommitAsync(string directory, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "rev-parse", "HEAD"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }

    public async Task<string> ReadCurrentBranchAsync(string directory, CancellationToken cancellationToken = default)
    {
        var current = await _runner.RunAsync(
                "git",
                ["-C", directory, "branch", "--show-current"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (current.Success && !string.IsNullOrWhiteSpace(current.StdOut))
        {
            return current.StdOut.Trim();
        }

        var symbolic = await _runner.RunAsync(
                "git",
                ["-C", directory, "symbolic-ref", "--short", "HEAD"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return symbolic.Success ? symbolic.StdOut.Trim() : "";
    }

    public async Task<IReadOnlyList<GitChange>> ReadWorkTreeChangesAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (!HasRepo(directory))
        {
            return [];
        }

        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "status", "--porcelain", "-uall"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return [];
        }

        var changes = new List<GitChange>();
        foreach (var line in result.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 4)
            {
                continue;
            }

            var path = line[3..].Trim().Trim('"');
            var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                path = path[(arrow + 4)..].Trim();
            }

            var name = Path.GetFileName(path.Replace('\\', '/'));
            if (IsMarkerName(name))
            {
                continue;
            }

            var code = line[..2];
            var kind = code.Contains('D', StringComparison.Ordinal)
                ? GitChangeKind.Deleted
                : code.Contains('A', StringComparison.Ordinal) || code.Contains('?', StringComparison.Ordinal)
                    ? GitChangeKind.Added
                    : GitChangeKind.Modified;
            changes.Add(new GitChange { Kind = kind, Path = path.Replace('\\', '/') });
        }

        return changes;
    }

    public async Task<bool> HasCommitAsync(
        string directory,
        string sha,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sha) || !HasRepo(directory))
        {
            return false;
        }

        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "cat-file", "-e", sha + "^{commit}"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success;
    }

    public async Task<bool?> MatchesCommitAsync(
        string directory,
        string sha,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sha) || !HasRepo(directory))
        {
            return null;
        }

        var diff = await _runner.RunAsync(
                "git",
                ["-C", directory, "diff", "--quiet", sha],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (diff.ExitCode is not 0 and not 1)
        {
            return null;
        }

        if (diff.ExitCode == 1)
        {
            return false;
        }

        var extra = await ReadWorkTreeChangesAsync(directory, cancellationToken).ConfigureAwait(false);
        return extra.All(change => change.Kind != GitChangeKind.Added);
    }

    private static bool IsMarkerName(string name) =>
        name.Equals(SkillInstaller.MarkerFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(SkillInstaller.PluginMarkerFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(SkillInstaller.PackageMarkerFileName, StringComparison.OrdinalIgnoreCase);
}
