using System.Collections.ObjectModel;
using MlsmoonSkillManager.App.Controls;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class SkillRowViewModel : ObservableObject
{
    private RepoAccess _access = new();
    private string _installSummary = "未扫描";
    private string _readmeExcerpt = "";
    private string _selectedBranch = "";
    private IReadOnlyList<RootInstallStatus> _installs = [];
    private SkillGitStatus _git = SkillGitStatus.Empty;
    private bool _suppressBranch;

    public SkillRowViewModel(SkillDefinition definition)
    {
        Definition = definition;
        EngineTags = BuildEngineTags(definition);
        _suppressBranch = true;
        foreach (var branch in definition.Branches.Select(item => item.Name).Where(item => item.Length > 0))
        {
            if (!Branches.Contains(branch, StringComparer.OrdinalIgnoreCase))
            {
                Branches.Add(branch);
            }
        }

        if (Branches.Count > 0)
        {
            _selectedBranch = SkillGitRecommend(definition);
        }

        _suppressBranch = false;
    }

    public event EventHandler? SelectedBranchChanged;

    public SkillDefinition Definition { get; }
    public string Name => Definition.DisplayName;
    public string Repo => Definition.Repo;
    public string Description =>
        string.IsNullOrWhiteSpace(ReadmeExcerpt) ? Definition.Description : ReadmeExcerpt;
    public bool IsLan => Definition.IsLan;
    public bool IsPlugin => Definition.IsPlugin;
    public bool IsCompanion => Definition.IsCompanion;
    public string KindLabel => Definition.IsLan
        ? "局域网"
        : Definition.IsPlugin
            ? "Plugin"
            : Definition.IsCompanion
                ? "随插件"
                : "Skill";
    public BadgeAppearance KindAppearance => Definition.IsLan
        ? BadgeAppearance.Warning
        : Definition.IsPlugin
            ? BadgeAppearance.Plugin
            : Definition.IsCompanion
                ? BadgeAppearance.Neutral
                : BadgeAppearance.Accent;
    public IReadOnlyList<EngineTagViewModel> EngineTags { get; }
    public string EngineLabel => Definition.IsUniversal
        ? "全引擎"
        : string.Join(" · ", Definition.ResolvedEngines.Select(GameEngines.Label));
    public ObservableCollection<string> Branches { get; } = [];
    public bool ShowInstall => !IsCompanion && !IsInstalledManaged;
    public bool ShowUpdate => IsInstalledManaged;
    public bool ShowOpenFolder => IsInstalled;
    public bool ShowCompanionHint => IsPlugin && Definition.CompanionSkills.Count > 0;
    public bool ShowBranchPicker => Branches.Count > 0;
    public bool ShowGitStatus => Git.State is not SkillGitState.Unknown and not SkillGitState.NotInstalled
        || !string.IsNullOrWhiteSpace(Git.Message);
    public bool ShowLocalChanges => Git.Changes.Count > 0;
    public bool ShowBranchWarning => !string.IsNullOrWhiteSpace(Git.Warning);
    public string CompanionHint =>
        ShowCompanionHint
            ? "安装时会一并写入 Skill 目标：" + string.Join("、", Definition.CompanionSkills.Select(item => item.DisplayName))
            : "";
    public string ParentHint =>
        IsCompanion && !string.IsNullOrWhiteSpace(Definition.ParentPluginName)
            ? $"属于 Plugin {Definition.ParentPluginName}，装完插件后才会出现。"
            : "";

    public RepoAccess Access
    {
        get => _access;
        set
        {
            if (SetProperty(ref _access, value))
            {
                Raise(nameof(AccessText));
                Raise(nameof(AccessTone));
                Raise(nameof(AccessBrushKey));
                Raise(nameof(ShowDetails));
                Raise(nameof(DeniedOnly));
            }
        }
    }

    public IReadOnlyList<RootInstallStatus> Installs
    {
        get => _installs;
        set
        {
            _installs = value;
            Raise();
            Raise(nameof(IsInstalled));
            Raise(nameof(IsInstalledManaged));
            Raise(nameof(ShowInstall));
            Raise(nameof(ShowUpdate));
            Raise(nameof(ShowOpenFolder));
            Raise(nameof(InstallFolder));
            Raise(nameof(InstallSummary));
        }
    }

    public string InstallSummary
    {
        get => _installSummary;
        set => SetProperty(ref _installSummary, value);
    }

    public string ReadmeExcerpt
    {
        get => _readmeExcerpt;
        set
        {
            if (SetProperty(ref _readmeExcerpt, value))
            {
                Raise(nameof(Description));
            }
        }
    }

    public string SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (SetProperty(ref _selectedBranch, value ?? "") && !_suppressBranch)
            {
                SelectedBranchChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public SkillGitStatus Git
    {
        get => _git;
        private set
        {
            _git = value;
            Raise();
            Raise(nameof(GitStatusText));
            Raise(nameof(GitStatusTone));
            Raise(nameof(ShowGitStatus));
            Raise(nameof(ShowLocalChanges));
            Raise(nameof(ShowBranchWarning));
            Raise(nameof(LocalChangesText));
            Raise(nameof(UpdateLabel));
            Raise(nameof(CanApplyUpdate));
        }
    }

    public bool IsInstalled => Installs.Any(item => item.Installed);
    public bool IsInstalledManaged => Installs.Any(item => item.Installed && item.Managed);
    public bool CanApplyUpdate => Git.CanUpdate && Access.CanInstall;
    public string InstallFolder =>
        Installs.FirstOrDefault(item => item.Installed)?.Path ?? "";

    public string GitStatusText => string.IsNullOrWhiteSpace(Git.Message)
        ? ""
        : Git.Message;

    public BadgeAppearance GitStatusTone => Git.State switch
    {
        SkillGitState.Current => BadgeAppearance.Success,
        SkillGitState.Behind => BadgeAppearance.Accent,
        SkillGitState.BranchSwitch => BadgeAppearance.Accent,
        SkillGitState.Checking => BadgeAppearance.Neutral,
        SkillGitState.LocalChanges => BadgeAppearance.Warning,
        SkillGitState.Conflict => BadgeAppearance.Danger,
        SkillGitState.Unmanaged => BadgeAppearance.Warning,
        _ => BadgeAppearance.Neutral
    };

    public string LocalChangesText
    {
        get
        {
            if (Git.Changes.Count == 0)
            {
                return "";
            }

            var lines = Git.Changes.Take(8).Select(item => item.Label);
            var text = string.Join(Environment.NewLine, lines);
            if (Git.Changes.Count > 8)
            {
                text += Environment.NewLine + $"还有 {Git.Changes.Count - 8} 个文件";
            }

            return text;
        }
    }

    public string UpdateLabel => Git.State switch
    {
        SkillGitState.Current => "已是最新",
        SkillGitState.Behind => "更新",
        SkillGitState.BranchSwitch => "切换分支",
        SkillGitState.LocalChanges => "有本地修改",
        SkillGitState.Conflict => "有冲突",
        SkillGitState.Checking => "对照中",
        _ => "更新"
    };

    public bool ShowDetails => Access.State is AccessState.Accessible or AccessState.Checking or AccessState.Unknown;

    public bool DeniedOnly => Access.State is AccessState.NoPermission
        or AccessState.GhMissing
        or AccessState.GhNotLoggedIn
        or AccessState.OffNetwork
        or AccessState.Unreachable
        or AccessState.NeedsAuth;

    public string AccessText => Access.State switch
    {
        AccessState.Checking => "正在检查权限…",
        AccessState.Accessible => string.IsNullOrWhiteSpace(Access.Visibility)
            ? "可访问"
            : $"可访问 · {Access.Visibility}",
        AccessState.NoPermission => string.IsNullOrWhiteSpace(Access.Message) ? "当前无权限访问" : Access.Message,
        AccessState.OffNetwork => "不在该局域网",
        AccessState.Unreachable => "NAS 不可达",
        AccessState.NeedsAuth => "NAS 需要 SSH 密码或密钥",
        AccessState.GhMissing => "未检测到 gh",
        AccessState.GhNotLoggedIn => "gh 未登录",
        _ => "待检查"
    };

    public BadgeAppearance AccessTone => ToneFor(Access.State);

    public string AccessBrushKey => Access.State switch
    {
        AccessState.Accessible => "OkBrush",
        AccessState.Checking => "MutedBrush",
        AccessState.Unknown => "MutedBrush",
        AccessState.GhNotLoggedIn => "WarnBrush",
        AccessState.OffNetwork => "WarnBrush",
        AccessState.NeedsAuth => "WarnBrush",
        AccessState.Unreachable => "BadBrush",
        _ => "BadBrush"
    };

    public static BadgeAppearance ToneFor(AccessState state) => state switch
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

    public void MarkGitChecking()
    {
        Git = new SkillGitStatus
        {
            State = SkillGitState.Checking,
            TargetBranch = SelectedBranch,
            InstalledBranch = Git.InstalledBranch,
            Branches = Branches.ToList(),
            Message = "正在对照 Git…"
        };
    }

    public void ApplyGit(SkillGitStatus status)
    {
        _suppressBranch = true;
        try
        {
            Branches.Clear();
            foreach (var branch in status.Branches)
            {
                if (!string.IsNullOrWhiteSpace(branch)
                    && !Branches.Contains(branch, StringComparer.OrdinalIgnoreCase))
                {
                    Branches.Add(branch);
                }
            }

            if (!string.IsNullOrWhiteSpace(status.TargetBranch))
            {
                _selectedBranch = status.TargetBranch;
                Raise(nameof(SelectedBranch));
            }

            Git = status;
            Raise(nameof(ShowBranchPicker));
        }
        finally
        {
            _suppressBranch = false;
        }
    }

    public void RefreshInstallSummary()
    {
        if (Installs.Count == 0)
        {
            InstallSummary = "未打开工作区，仅可浏览";
            return;
        }

        var parts = Installs.Select(item =>
        {
            if (!item.Installed)
            {
                return $"{item.Root}: 未安装";
            }

            var shortCommit = item.Commit.Length >= 7 ? item.Commit[..7] : item.Commit;
            var branch = string.IsNullOrWhiteSpace(item.Branch) ? "" : item.Branch + " · ";
            var extra = item.Managed
                ? (string.IsNullOrWhiteSpace(shortCommit) ? "已托管" : branch + shortCommit)
                : "本地存在但非本工具安装";
            return $"{item.Root}: {extra}";
        });
        var summary = string.Join("  ·  ", parts);
        InstallSummary = IsCompanion
            ? $"随插件 Skill  ·  {summary}"
            : summary;
    }

    private static string SkillGitRecommend(SkillDefinition definition)
    {
        return MlsmoonSkillManager.Core.Services.SkillGit.RecommendBranch(definition, null, null);
    }

    private static IReadOnlyList<EngineTagViewModel> BuildEngineTags(SkillDefinition definition)
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

public sealed record EngineTagViewModel(string Label, BadgeAppearance Appearance);
