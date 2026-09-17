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

    public static string ActionLabel(SkillGitState state) => state switch
    {
        SkillGitState.Current => "已对齐",
        SkillGitState.Behind => "更新到远端",
        SkillGitState.Ahead => "本机超前",
        SkillGitState.Diverged => "已分叉",
        SkillGitState.Unclear => "无法判断",
        SkillGitState.BranchSwitch => "切换分支",
        SkillGitState.LocalChanges => "有本地修改",
        SkillGitState.Conflict => "有冲突",
        SkillGitState.Checking => "对照中",
        _ => "对照 Git"
    };
}
