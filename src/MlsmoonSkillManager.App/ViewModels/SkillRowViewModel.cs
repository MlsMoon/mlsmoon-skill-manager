using System.Collections.ObjectModel;
using MlsmoonSkillManager.App.Controls;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

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
    private RoutingSkillAlign _routing = new();
    private bool _suppressBranch;
    private bool _userPickedBranch;
    private bool _allowGitPush;
    private bool _allowGitCommit;

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
    public bool IsRouting => Definition.IsRouting;
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
    public bool ShowBranchPicker => !IsRouting && Branches.Count > 0;
    public bool ShowGitActions => !IsRouting;
    public bool ShowGitStatus => !IsRouting
        && (Git.State is not SkillGitState.Unknown and not SkillGitState.NotInstalled
            || !string.IsNullOrWhiteSpace(Git.Message));
    public bool ShowLocalChanges => IsRouting ? Routing.Changes.Count > 0 : Git.Changes.Count > 0;
    public bool ShowSourceSync => IsRouting;
    public bool ShowSourceStatus => IsRouting && !IsLoading;
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
                Raise(nameof(CanCommit));
                Raise(nameof(CanApplyUpdate));
                Raise(nameof(CanInitGit));
                Raise(nameof(ShowInitGit));
                Raise(nameof(CanChangeBranch));
                Raise(nameof(ShowGitActions));
                Raise(nameof(ShowSourceSync));
                Raise(nameof(ShowSourceStatus));
                Raise(nameof(CanSyncFromSource));
                Raise(nameof(CanSyncToSource));
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
            Raise(nameof(CanSyncFromSource));
            Raise(nameof(CanSyncToSource));
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
                if (Git.State is SkillGitState.Unknown or SkillGitState.Checking)
                {
                    return;
                }

                _userPickedBranch = true;
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
            Raise(nameof(CanCommit));
            Raise(nameof(CanApplyUpdate));
            Raise(nameof(CanInitGit));
            Raise(nameof(ShowInitGit));
            Raise(nameof(CanChangeBranch));
            Raise(nameof(IsLoading));
            Raise(nameof(LoadText));
            Raise(nameof(ShowGitActions));
            Raise(nameof(ShowSourceStatus));
            Raise(nameof(ShowSourceSync));
        }
    }

    public RoutingSkillAlign Routing
    {
        get => _routing;
        private set
        {
            _routing = value;
            Raise();
            Raise(nameof(SourceStatusText));
            Raise(nameof(SourceStatusTone));
            Raise(nameof(ShowSourceStatus));
            Raise(nameof(ShowSourceSync));
            Raise(nameof(CanSyncFromSource));
            Raise(nameof(CanSyncToSource));
            Raise(nameof(ShowLocalChanges));
            Raise(nameof(LocalChangesText));
        }
    }

    public bool IsInstalled => Installs.Any(item => item.Installed);
    public bool IsInstalledManaged => Installs.Any(item => item.Installed && item.Managed);
    public bool IsLoading =>
        !string.IsNullOrWhiteSpace(_loadText)
        || Access.State == AccessState.Checking
        || Git.State == SkillGitState.Checking;
    public bool GitReady => Git.State != SkillGitState.NeedsAttach;
    public bool CanApplyUpdate => !IsRouting && GitReady && SkillGitPresentation.CanPull(Git, Access.CanInstall);
    public bool CanPull => CanApplyUpdate;
    public bool CanPush => !IsRouting && GitReady && SkillGitPresentation.CanPush(Git, Access.CanInstall, _allowGitPush);
    public bool CanCommit => !IsRouting && GitReady && SkillGitPresentation.CanCommit(Git, Access.CanInstall, _allowGitCommit);
    public bool ShowInitGit => !IsRouting && Git.State == SkillGitState.NeedsAttach;
    public bool CanInitGit => ShowInitGit && Access.CanInstall && !Git.Forbidden;
    public bool HasUserPickedBranch => _userPickedBranch;
    public bool CanChangeBranch => !IsRouting && GitReady && !IsLoading;
    public bool CanSyncFromSource => IsRouting && Routing.SourceExists;
    public bool CanSyncToSource => IsRouting && (Routing.DestExists || IsInstalled);
    public string InstallFolder =>
        Installs.FirstOrDefault(item => item.Installed)?.Path ?? "";
    public string LoadText => !string.IsNullOrWhiteSpace(_loadText)
        ? _loadText
        : Access.State == AccessState.Checking
            ? "正在检查权限…"
            : Git.State == SkillGitState.Checking
                ? IsRouting ? "对照路由源…" : "正在对照 Git…"
                : "";
    public double LoadProgress => _loadProgress;

    public string GitStatusText => string.IsNullOrWhiteSpace(Git.Message)
        ? ""
        : Git.Message;

    public BadgeAppearance GitStatusTone => SkillGitPresentation.Tone(Git.State);
    public string SourceStatusText => Routing.Summary;
    public BadgeAppearance SourceStatusTone =>
        !Routing.SourceExists
            ? BadgeAppearance.Warning
            : Routing.Changes.Count == 0 && Routing.DestExists
                ? BadgeAppearance.Success
                : BadgeAppearance.Warning;
    public string LocalChangesText => IsRouting
        ? SkillRowPresentation.LocalChanges(Routing.Changes)
        : SkillRowPresentation.LocalChanges(Git);
    public string PullLabel => SkillGitPresentation.PullLabel(Git);
    public string CommitLabel => "Commit";
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

    public void SetGitWriteFlags(bool allowPush, bool allowCommit)
    {
        _allowGitPush = allowPush;
        _allowGitCommit = allowCommit;
        Raise(nameof(CanPush));
        Raise(nameof(CanCommit));
        Raise(nameof(CanInitGit));
    }

    public void SetScan(string text, int percent)
    {
        _loadText = text;
        _loadProgress = Math.Clamp(percent, 0, 100);
        Raise(nameof(LoadText));
        Raise(nameof(LoadProgress));
        Raise(nameof(IsLoading));
        Raise(nameof(ShowSourceStatus));
        Raise(nameof(CanChangeBranch));
    }

    public void ClearScan()
    {
        _loadText = "";
        _loadProgress = 0;
        Raise(nameof(LoadText));
        Raise(nameof(LoadProgress));
        Raise(nameof(IsLoading));
        Raise(nameof(ShowSourceStatus));
        Raise(nameof(CanChangeBranch));
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

    public void ApplyRouting(RoutingSkillAlign align)
    {
        Routing = align;
        ApplyGit(SkillGitStatus.Empty);
        Raise(nameof(ShowSourceStatus));
        Raise(nameof(CanSyncFromSource));
        Raise(nameof(CanSyncToSource));
    }

    public void ApplyGit(SkillGitStatus status)
    {
        _suppressBranch = true;
        try
        {
            if (status.Branches.Count > 0)
            {
                foreach (var branch in status.Branches)
                {
                    if (!string.IsNullOrWhiteSpace(branch)
                        && !Branches.Contains(branch, StringComparer.OrdinalIgnoreCase))
                    {
                        Branches.Add(branch);
                    }
                }

                for (var i = Branches.Count - 1; i >= 0; i--)
                {
                    if (!status.Branches.Contains(Branches[i], StringComparer.OrdinalIgnoreCase))
                    {
                        Branches.RemoveAt(i);
                    }
                }
            }

            _userPickedBranch = false;
            var shown = status.InstalledBranch;
            if (string.IsNullOrWhiteSpace(shown))
            {
                shown = status.TargetBranch;
            }

            if (!string.IsNullOrWhiteSpace(shown))
            {
                _selectedBranch = shown;
            }

            Raise(nameof(SelectedBranch));

            Git = status;
            _loadText = "";
            _loadProgress = 0;
            Raise(nameof(ShowBranchPicker));
            Raise(nameof(CanChangeBranch));
            Raise(nameof(LoadText));
            Raise(nameof(LoadProgress));
            RefreshInstallSummary();
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
        Raise(nameof(ShowCompanionHint));
        Raise(nameof(KindLabel));
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

            var shownBranch = !string.IsNullOrWhiteSpace(Git.InstalledBranch) ? Git.InstalledBranch : item.Branch;
            var shortCommit = !string.IsNullOrWhiteSpace(Git.InstalledCommit)
                ? (Git.InstalledCommit.Length >= 7 ? Git.InstalledCommit[..7] : Git.InstalledCommit)
                : (item.Commit.Length >= 7 ? item.Commit[..7] : item.Commit);
            var branch = string.IsNullOrWhiteSpace(shownBranch) ? "" : shownBranch + " · ";
            var extra = string.IsNullOrWhiteSpace(shortCommit)
                ? (string.IsNullOrWhiteSpace(shownBranch) ? "已安装" : shownBranch)
                : branch + shortCommit;
            return $"{item.Root}: {extra}";
        });
        var summary = string.Join("  ·  ", parts);
        InstallSummary = Definition.IsRouting
            ? $"路由 Skill  ·  {summary}"
            : IsCompanion
                ? $"随插件 Skill  ·  {summary}"
                : summary;
    }

    public void RevertBranchToInstalled()
    {
        _suppressBranch = true;
        try
        {
            _userPickedBranch = false;
            var fallback = Git.InstalledBranch;
            if (string.IsNullOrWhiteSpace(fallback))
            {
                fallback = Git.TargetBranch;
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                _selectedBranch = fallback;
                Raise(nameof(SelectedBranch));
            }
        }
        finally
        {
            _suppressBranch = false;
        }
    }

    public void ClearUserBranchPick()
    {
        _userPickedBranch = false;
    }
}

public sealed record EngineTagViewModel(string Label, BadgeAppearance Appearance);
