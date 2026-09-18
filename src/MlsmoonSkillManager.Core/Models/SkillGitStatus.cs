namespace MlsmoonSkillManager.Core.Models;

public enum SkillGitState
{
    Unknown = 0,
    NotInstalled,
    Checking,
    Current,
    Behind,
    Ahead,
    Diverged,
    Unclear,
    LocalChanges,
    Conflict,
    BranchSwitch
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
    public CommitCompare Compare { get; init; } = CommitCompare.Same;
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

    public static SkillGitState Decide(
        bool installed,
        bool hasLocal,
        CommitRelation relation,
        bool branchDiffers)
    {
        if (!installed)
        {
            return SkillGitState.NotInstalled;
        }

        var remoteHasNew = relation is CommitRelation.LocalBehind or CommitRelation.Diverged;
        if (hasLocal && (remoteHasNew || branchDiffers))
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

        return relation switch
        {
            CommitRelation.LocalBehind => SkillGitState.Behind,
            CommitRelation.LocalAhead => SkillGitState.Ahead,
            CommitRelation.Diverged => SkillGitState.Diverged,
            CommitRelation.Unknown => SkillGitState.Unclear,
            _ => SkillGitState.Current
        };
    }

    public static string Describe(
        SkillGitState state,
        CommitCompare compare,
        string target,
        string installedBranch,
        string installedCommit,
        string remoteCommit,
        int changeCount,
        bool canReachRemote,
        bool hasRemote)
    {
        var local = ShortSha(installedCommit);
        var remote = ShortSha(remoteCommit);
        var branch = string.IsNullOrWhiteSpace(target) ? installedBranch : target;
        var commits = FormatCommits(local, remote, branch);
        return state switch
        {
            SkillGitState.NotInstalled => "",
            SkillGitState.Checking => "正在对照 Git…",
            SkillGitState.Current => string.IsNullOrWhiteSpace(local)
                ? $"已与远端对齐{(string.IsNullOrWhiteSpace(branch) ? "" : " · " + branch)}"
                : $"已与远端对齐 · {branch} · {local}",
            SkillGitState.Behind => compare.BehindBy > 0
                ? $"↓ Pull · 本机落后远端 {compare.BehindBy} 个提交{Nl}{commits}"
                : $"↓ Pull · 本机落后远端{Nl}{commits}",
            SkillGitState.Ahead => compare.AheadBy > 0
                ? $"↑ Push · 本机超前远端 {compare.AheadBy} 个提交{Nl}{commits}"
                : $"↑ Push · 本机超前远端{Nl}{commits}",
            SkillGitState.Diverged => ArrowCounts(compare) + " · 已分叉，不能快进" + Nl + commits,
            SkillGitState.Unclear => !hasRemote
                ? canReachRemote ? "未能读取远端提交" : "尚未对照远端"
                : $"本机与远端提交不同，无法判断谁新{Nl}{commits}",
            SkillGitState.LocalChanges => compare.Relation == CommitRelation.LocalAhead
                ? $"远端落后本机，工作区另有 {changeCount} 处本地修改"
                : $"远端没有新提交，工作区有 {changeCount} 处本地修改",
            SkillGitState.Conflict => compare.Relation == CommitRelation.Diverged
                ? "有冲突：工作区改过，且本机与远端已分叉，请手动处理"
                : "有冲突：工作区改过，且本机落后远端，请手动处理后再更新",
            SkillGitState.BranchSwitch => $"将切换到 {branch}，工作区是干净的",
            _ => canReachRemote && !hasRemote ? "未能读取远端提交" : "尚未对照 Git"
        };
    }

    public static string ShortSha(string sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return "";
        }

        return sha.Length >= 7 ? sha[..7] : sha;
    }

    private static string ArrowCounts(CommitCompare compare)
    {
        var down = compare.BehindBy > 0 ? $"↓ {compare.BehindBy}" : "↓";
        var up = compare.AheadBy > 0 ? $"↑ {compare.AheadBy}" : "↑";
        return down + "  " + up;
    }

    private static string FormatCommits(string local, string remote, string branch)
    {
        var suffix = string.IsNullOrWhiteSpace(branch) ? "" : "  ·  " + branch;
        if (local.Length == 0 && remote.Length == 0)
        {
            return branch;
        }

        if (local.Length == 0)
        {
            return "远端 " + remote + suffix;
        }

        if (remote.Length == 0)
        {
            return "本机 " + local + suffix;
        }

        return "本机 " + local + "  ·  远端 " + remote + suffix;
    }

    private const string Nl = "\n";
}
