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

    public static bool CanPush(SkillGitStatus git, bool canInstall) =>
        canInstall && git.State == SkillGitState.Ahead;
}
