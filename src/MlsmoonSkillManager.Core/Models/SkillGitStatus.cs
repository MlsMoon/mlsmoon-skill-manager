namespace MlsmoonSkillManager.Core.Models;

public enum SkillGitState
{
    Unknown = 0,
    NotInstalled,
    Checking,
    Current,
    Behind,
    LocalChanges,
    Conflict,
    BranchSwitch,
    Unmanaged
}

public enum GitChangeKind
{
    Modified,
    Added,
    Deleted
}

public sealed class GitChange
{
    public GitChangeKind Kind { get; init; }
    public string Path { get; init; } = "";

    public string Label => Kind switch
    {
        GitChangeKind.Modified => "M  " + Path,
        GitChangeKind.Added => "A  " + Path,
        GitChangeKind.Deleted => "D  " + Path,
        _ => Path
    };
}

public sealed class SkillGitStatus
{
    public static SkillGitStatus Empty { get; } = new();

    public SkillGitState State { get; init; } = SkillGitState.Unknown;
    public string InstalledCommit { get; init; } = "";
    public string RemoteCommit { get; init; } = "";
    public string InstalledBranch { get; init; } = "";
    public string TargetBranch { get; init; } = "";
    public IReadOnlyList<string> Branches { get; init; } = [];
    public IReadOnlyList<GitChange> Changes { get; init; } = [];
    public string Message { get; init; } = "";
    public string Warning { get; init; } = "";
    public bool Forbidden { get; init; }
    public bool CanUpdate =>
        !Forbidden && State is SkillGitState.Behind or SkillGitState.BranchSwitch;

    public static SkillGitState Decide(bool installed, bool managed, bool hasLocal, bool remoteAhead, bool branchDiffers)
    {
        if (!installed)
        {
            return SkillGitState.NotInstalled;
        }

        if (!managed)
        {
            return SkillGitState.Unmanaged;
        }

        if (hasLocal && (remoteAhead || branchDiffers))
        {
            return SkillGitState.Conflict;
        }

        if (hasLocal)
        {
            return SkillGitState.LocalChanges;
        }

        if (branchDiffers)
        {
            return SkillGitState.BranchSwitch;
        }

        if (remoteAhead)
        {
            return SkillGitState.Behind;
        }

        return SkillGitState.Current;
    }

    public static string ShortSha(string sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return "";
        }

        return sha.Length >= 7 ? sha[..7] : sha;
    }
}
