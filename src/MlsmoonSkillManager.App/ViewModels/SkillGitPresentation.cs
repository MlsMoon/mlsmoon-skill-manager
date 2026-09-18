using MlsmoonSkillManager.App.Controls;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.App.ViewModels;

public static class SkillGitPresentation
{
    public static BadgeAppearance Tone(SkillGitState state) => state switch
    {
        SkillGitState.Current => BadgeAppearance.Success,
        SkillGitState.Behind => BadgeAppearance.Accent,
        SkillGitState.BranchSwitch => BadgeAppearance.Accent,
        SkillGitState.Ahead => BadgeAppearance.Warning,
        SkillGitState.Unclear => BadgeAppearance.Warning,
        SkillGitState.Checking => BadgeAppearance.Neutral,
        SkillGitState.LocalChanges => BadgeAppearance.Warning,
        SkillGitState.Diverged => BadgeAppearance.Danger,
        SkillGitState.Conflict => BadgeAppearance.Danger,
        SkillGitState.NeedsAttach => BadgeAppearance.Accent,
        _ => BadgeAppearance.Neutral
    };

    public static string PullLabel(SkillGitStatus git)
    {
        var n = git.Compare.BehindBy;
        return n > 0 ? $"↓ {n}" : "↓ Pull";
    }

    public static string PushLabel(SkillGitStatus git)
    {
        var n = git.Compare.AheadBy;
        return n > 0 ? $"↑ {n}" : "↑ Push";
    }

    public static bool CanPull(SkillGitStatus git, bool canInstall) =>
        canInstall && git.CanUpdate;

    public static bool CanPush(SkillGitStatus git, bool canInstall, bool allowPush) =>
        canInstall
        && allowPush
        && !git.Forbidden
        && git.State == SkillGitState.Ahead;

    public static bool CanCommit(SkillGitStatus git, bool canInstall, bool allowCommit) =>
        canInstall
        && allowCommit
        && !git.Forbidden
        && git.State is SkillGitState.LocalChanges or SkillGitState.Conflict;

    public static string PullConfirm(string name, string branch, int behindBy)
    {
        var origin = string.IsNullOrWhiteSpace(branch) ? "远端分支" : "origin/" + branch;
        var count = behindBy > 0 ? $"本机落后 {behindBy} 个提交。" + Environment.NewLine + Environment.NewLine : "";
        return $"{name} 会 fetch 远端，再 merge --ff-only 接到 {origin}。"
            + Environment.NewLine + Environment.NewLine
            + count
            + "只快进，不会覆盖未提交改动。工作区不干净或已经分叉时这次不会开始。";
    }

    public static string PushConfirm(string name, string branch, int aheadBy)
    {
        var origin = string.IsNullOrWhiteSpace(branch) ? "远端分支" : "origin/" + branch;
        var count = aheadBy > 0 ? $"本机超前 {aheadBy} 个提交。" + Environment.NewLine + Environment.NewLine : "";
        return $"{name} 会把本机已有提交推到 {origin}。"
            + Environment.NewLine + Environment.NewLine
            + count
            + "不会提交工作区里还没 commit 的改动。推上去之后不能从本工具一键撤销。";
    }

    public static string BranchSwitchWarning(string name, string from, string to, bool forbiddenTarget)
    {
        var text =
            $"{name} 当前在分支「{from}」，你选择了「{to}」。"
            + Environment.NewLine + Environment.NewLine
            + "切换会立刻改掉安装目录里已跟踪的文件，不能从本工具一键撤销。"
            + "有未提交改动时会拒绝切换。当前分支上的提交还在，只是工作区会变成目标分支的内容。"
            + Environment.NewLine + Environment.NewLine
            + "Unity 会重新导入这套文件。URP 版本不同的分支不能混用，切错后工程可能打不开。";
        if (forbiddenTarget)
        {
            text += Environment.NewLine + Environment.NewLine
                    + "Unity 6 不能切到 master（URP 14）。请留在 urp-17.5，或先处理工程版本。";
        }

        return text;
    }
}
