namespace MlsmoonSkillManager.Core.Services;

public sealed class WorkspaceGit
{
    private readonly IProcessRunner _runner;

    public WorkspaceGit(IProcessRunner runner)
    {
        _runner = runner;
    }

    public static bool HasRepo(string directory) => SkillCopy.HasGitRepo(directory);

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
        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "branch", "--show-current"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
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

        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "diff", "--quiet", sha],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => null
        };
    }
}
