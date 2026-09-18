using MlsmoonSkillManager.App.Controls;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.App.ViewModels;

public static class SkillRowPresentation
{
    public static string KindLabel(SkillDefinition definition) => definition.IsPackage
        ? "Package"
        : definition.IsPlugin
            ? "Plugin"
            : definition.IsCompanion
                ? "随插件"
                : definition.IsLan
                    ? "局域网"
                    : "Skill";

    public static BadgeAppearance KindAppearance(SkillDefinition definition) => definition.IsPackage
        ? BadgeAppearance.Package
        : definition.IsPlugin
            ? BadgeAppearance.Plugin
            : definition.IsCompanion
                ? BadgeAppearance.Neutral
                : definition.IsLan
                    ? BadgeAppearance.Warning
                    : BadgeAppearance.Accent;

    public static string AccessText(RepoAccess access) => access.State switch
    {
        AccessState.Checking => "正在检查权限…",
        AccessState.Accessible => string.IsNullOrWhiteSpace(access.Visibility)
            ? "可访问"
            : $"可访问 · {access.Visibility}",
        AccessState.NoPermission => string.IsNullOrWhiteSpace(access.Message) ? "当前无权限访问" : access.Message,
        AccessState.OffNetwork => "不在该局域网",
        AccessState.Unreachable => "NAS 不可达",
        AccessState.NeedsAuth => "NAS 需要 SSH 密码或密钥",
        AccessState.GhMissing => "未检测到 gh",
        AccessState.GhNotLoggedIn => "gh 未登录",
        _ => "待检查"
    };

    public static BadgeAppearance AccessTone(AccessState state) => state switch
    {
        AccessState.Accessible => BadgeAppearance.Success,
        AccessState.Checking => BadgeAppearance.Neutral,
        AccessState.Unknown => BadgeAppearance.Neutral,
        AccessState.GhNotLoggedIn => BadgeAppearance.Warning,
        AccessState.OffNetwork => BadgeAppearance.Warning,
        AccessState.NeedsAuth => BadgeAppearance.Warning,
        AccessState.Unreachable => BadgeAppearance.Danger,
        _ => BadgeAppearance.Danger
    };

    public static string LocalChanges(SkillGitStatus git)
    {
        if (git.Changes.Count == 0)
        {
            return "";
        }

        var lines = git.Changes.Take(8).Select(item => item.Label);
        var text = string.Join(Environment.NewLine, lines);
        if (git.Changes.Count > 8)
        {
            text += Environment.NewLine + $"还有 {git.Changes.Count - 8} 个文件";
        }

        return text;
    }

    public static string CompanionHint(SkillDefinition definition) =>
        definition.IsProjectCopy && definition.CompanionSkills.Count > 0
            ? "安装时会一并写入 Skill 目标：" + string.Join("、", definition.CompanionSkills.Select(item => item.DisplayName))
            : "";

    public static string ParentHint(SkillDefinition definition) =>
        definition.IsCompanion && !string.IsNullOrWhiteSpace(definition.ParentPluginName)
            ? $"属于 {definition.ParentPluginName}，装完后才会出现。"
            : "";

    public static IReadOnlyList<EngineTagViewModel> EngineTags(SkillDefinition definition)
    {
        if (definition.IsUniversal)
        {
            return [new EngineTagViewModel("全引擎", BadgeAppearance.Universal)];
        }

        return definition.ResolvedEngines
            .Select(engine => new EngineTagViewModel(
                GameEngines.Label(engine),
                engine == GameEngines.Godot ? BadgeAppearance.Godot : BadgeAppearance.Unity))
            .ToList();
    }
}
