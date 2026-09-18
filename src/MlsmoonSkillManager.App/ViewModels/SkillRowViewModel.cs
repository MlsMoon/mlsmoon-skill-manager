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
    private string _loadText = "";
    private double _loadProgress;
    private IReadOnlyList<RootInstallStatus> _installs = [];
    private SkillGitStatus _git = SkillGitStatus.Empty;
    private bool _suppressBranch;

    public SkillRowViewModel(SkillDefinition definition)
    {
        Definition = definition;
        EngineTags = SkillRowPresentation.EngineTags(definition);
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
        OpenFolderCommand = new RelayCommand(_ => OpenFolderRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? SelectedBranchChanged;
    public event EventHandler? OpenFolderRequested;
    public RelayCommand OpenFolderCommand { get; }

    public SkillDefinition Definition { get; }
    public string Name => Definition.DisplayName;
    public string Repo => Definition.Repo;
    public string Description =>
        string.IsNullOrWhiteSpace(ReadmeExcerpt) ? Definition.Description : ReadmeExcerpt;
    public bool IsLan => Definition.IsLan;
    public bool IsPlugin => Definition.IsPlugin;
    public bool IsCompanion => Definition.IsCompanion;
    public string KindLabel => SkillRowPresentation.KindLabel(Definition);
    public BadgeAppearance KindAppearance => SkillRowPresentation.KindAppearance(Definition);
    public IReadOnlyList<EngineTagViewModel> EngineTags { get; }
    public string EngineLabel => Definition.IsUniversal
        ? "全引擎"
        : string.Join(" · ", Definition.ResolvedEngines.Select(GameEngines.Label));
    public ObservableCollection<string> Branches { get; } = [];
    public bool ShowInstall => !IsCompanion && !IsInstalled;
    public bool ShowOpenFolder => IsInstalled;
    public bool ShowCompanionHint => Definition.IsProjectCopy && Definition.CompanionSkills.Count > 0;
    public bool ShowBranchPicker => Branches.Count > 0;
    public bool ShowGitStatus => Git.State is not SkillGitState.Unknown and not SkillGitState.NotInstalled
        || !string.IsNullOrWhiteSpace(Git.Message);
    public bool ShowLocalChanges => Git.Changes.Count > 0;
    public bool ShowBranchWarning => !string.IsNullOrWhiteSpace(Git.Warning);
    public string CompanionHint => SkillRowPresentation.CompanionHint(Definition);
    public string ParentHint => SkillRowPresentation.ParentHint(Definition);

    public RepoAccess Access
    {
        get => _access;
        set
        {
            if (SetProperty(ref _access, value))
            {
                Raise(nameof(AccessText));
                Raise(nameof(AccessTone));
                Raise(nameof(ShowDetails));
                Raise(nameof(DeniedOnly));
                Raise(nameof(IsLoading));
                Raise(nameof(LoadText));
                Raise(nameof(CanPull));
                Raise(nameof(CanPush));
                Raise(nameof(CanApplyUpdate));
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
            Raise(nameof(PullLabel));
            Raise(nameof(PushLabel));
            Raise(nameof(CanPull));
            Raise(nameof(CanPush));
            Raise(nameof(CanApplyUpdate));
            Raise(nameof(IsLoading));
            Raise(nameof(LoadText));
        }
    }

    public bool IsInstalled => Installs.Any(item => item.Installed);
    public bool IsInstalledManaged => Installs.Any(item => item.Installed && item.Managed);
    public bool IsLoading =>
        Access.State == AccessState.Checking || Git.State == SkillGitState.Checking;
    public bool CanApplyUpdate => SkillGitPresentation.CanPull(Git, Access.CanInstall);
    public bool CanPull => CanApplyUpdate;
    public bool CanPush => SkillGitPresentation.CanPush(Git, Access.CanInstall);
    public string InstallFolder =>
        Installs.FirstOrDefault(item => item.Installed)?.Path ?? "";
    public string LoadText => !string.IsNullOrWhiteSpace(_loadText)
        ? _loadText
        : Access.State == AccessState.Checking
            ? "正在检查权限…"
            : Git.State == SkillGitState.Checking
                ? "正在对照 Git…"
                : "";
    public double LoadProgress => _loadProgress;

    public string GitStatusText => string.IsNullOrWhiteSpace(Git.Message)
        ? ""
        : Git.Message;

    public BadgeAppearance GitStatusTone => SkillGitPresentation.Tone(Git.State);
    public string LocalChangesText => SkillRowPresentation.LocalChanges(Git);
    public string PullLabel => SkillGitPresentation.PullLabel(Git);
    public string PushLabel => SkillGitPresentation.PushLabel(Git);
    public bool ShowDetails => Access.State is AccessState.Accessible or AccessState.Checking or AccessState.Unknown;
    public bool DeniedOnly => Access.State is AccessState.NoPermission
        or AccessState.GhMissing
        or AccessState.GhNotLoggedIn
        or AccessState.OffNetwork
        or AccessState.Unreachable
        or AccessState.NeedsAuth;
    public string AccessText => SkillRowPresentation.AccessText(Access);
    public BadgeAppearance AccessTone => SkillRowPresentation.AccessTone(Access.State);
    public static BadgeAppearance ToneFor(AccessState state) => SkillRowPresentation.AccessTone(state);

    public void SetScan(string text, int percent)
    {
        _loadText = text;
        _loadProgress = Math.Clamp(percent, 0, 100);
        Raise(nameof(LoadText));
        Raise(nameof(LoadProgress));
    }

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

    public void ApplyDisplay()
    {
        Raise(nameof(Name));
        Raise(nameof(Description));
        Raise(nameof(ParentHint));
        Raise(nameof(CompanionHint));
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
            var extra = string.IsNullOrWhiteSpace(shortCommit)
                ? (string.IsNullOrWhiteSpace(item.Branch) ? "已安装" : item.Branch)
                : branch + shortCommit;
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
}

public sealed record EngineTagViewModel(string Label, BadgeAppearance Appearance);
