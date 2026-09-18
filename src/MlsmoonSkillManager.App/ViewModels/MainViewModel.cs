using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using MlsmoonSkillManager.App.Controls;
using MlsmoonSkillManager.App.Theming;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly CatalogStore _catalog;
    private readonly SettingsStore _settingsStore;
    private readonly WorkspaceScanner _scanner;
    private readonly IProcessRunner _runner;
    private readonly GhCli _gh;
    private readonly GitRemote _git;
    private readonly SkillInstaller _installer;
    private readonly WorkspaceRepo _workspaceRepo;
    private readonly SkillGit _skillGit;
    private readonly UpdateService _updater;
    private readonly UserSettings _settings;
    private string _workspacePath = "";
    private string _ghStatus = "尚未检查 gh";
    private string _igpStatus = "尚未检查局域网";
    private string _nasStatus = "尚未检查 NAS";
    private string _nasUser = "";
    private string _settingsTab = "appearance";
    private string _updateStatus = "尚未检查应用更新";
    private bool _autoCheckUpdates;
    private bool _hasAppUpdate;
    private bool _updateBusy;
    private bool _isDownloadingUpdate;
    private double _updateProgress;
    private string _updateProgressText = "";
    private AppReleaseInfo? _latestRelease;
    private AccessState _ghState;
    private AccessState _igpState;
    private AccessState _nasState;
    private string _logText = "";
    private bool _busy;
    private bool _isLogOpen;
    private bool _isSettingsOpen;
    private bool _isConflictOpen;
    private string _conflictText = "";
    private string _conflictFolder = "";
    private bool _showRepoLinks;
    private bool _allowGitPush;
    private bool _allowGitCommit;
    private bool _isCommitOpen;
    private string _commitMessage = "";
    private string _commitChangesText = "";
    private SkillRowViewModel? _commitRow;
    private bool _isGitActionOpen;
    private string _gitActionTitle = "";
    private string _gitActionText = "";
    private string _gitActionPrimaryText = "确定";
    private GitCardAction _gitAction;
    private SkillRowViewModel? _gitActionRow;
    private bool _isInitGitOpen;
    private bool _initGitRunning;
    private string _initGitText = "";
    private double _initGitProgress;
    private string _initGitProgressText = "";
    private SkillRowViewModel? _initGitRow;
    private bool _isBranchSwitchOpen;
    private string _branchSwitchText = "";
    private SkillRowViewModel? _branchSwitchRow;
    private bool _branchSwitchConfirmed;
    private bool _suppressRootEvents;
    private GhAccountStatus _account = new();
    private SkillCatalogScan _scan = null!;

    public MainViewModel()
    {
        _paths = new AppPaths();
        _catalog = new CatalogStore(_paths);
        _settingsStore = new SettingsStore(_paths);
        _scanner = new WorkspaceScanner();
        _runner = new ProcessRunner();
        _gh = new GhCli(_runner);
        _git = new GitRemote(_runner);
        _installer = new SkillInstaller(_paths, _gh, _runner);
        _workspaceRepo = new WorkspaceRepo(_runner);
        _skillGit = new SkillGit(_paths, _gh, _git);
        _updater = new UpdateService();
        _settings = _settingsStore.Load();
        WorkspaceBook.Migrate(_settings);
        WorkspacePath = _settings.LastWorkspace;
        foreach (var root in SkillRoots.DetectableRoots)
        {
            Roots.Add(new RootOptionViewModel(root));
        }

        foreach (var tab in SettingsTabViewModel.CreateAll(IsDev))
        {
            SettingsTabs.Add(tab);
        }

        foreach (var theme in ThemeResolver.Preferences)
        {
            Themes.Add(new ThemeOptionViewModel(theme)
            {
                IsSelected = theme == ThemeResolver.Normalize(_settings.Theme)
            });
        }

        foreach (var skill in _catalog.Load().Skills)
        {
            var row = new SkillRowViewModel(skill);
            row.SelectedBranchChanged += OnSelectedBranchChanged;
            row.OpenFolderRequested += OnOpenFolderRequested;
            AllSkills.Add(row);
        }

        CatalogFilter.Changed += ApplyFilter;
        ApplyFilter();
        BrowseCommand = new RelayCommand(_ => AddWorkspace());
        SelectWorkspaceCommand = new RelayCommand(p => SelectWorkspace(p as WorkspaceItemViewModel));
        RemoveWorkspaceCommand = new RelayCommand(p => RemoveWorkspace(p as WorkspaceItemViewModel));
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync().ConfigureAwait(true), _ => !Busy);
        InstallCommand = new RelayCommand(async p => await InstallAsync(p as SkillRowViewModel).ConfigureAwait(true), CanMutate);
        PullCommand = new RelayCommand(async p => await PullAsync(p as SkillRowViewModel).ConfigureAwait(true), p => p is not SkillRowViewModel row || row.CanPull);
        PushCommand = new RelayCommand(async p => await PushAsync(p as SkillRowViewModel).ConfigureAwait(true), p => p is not SkillRowViewModel row || row.CanPush);
        CommitCommand = new RelayCommand(async p => await CommitAsync(p as SkillRowViewModel).ConfigureAwait(true), p => p is not SkillRowViewModel row || row.CanCommit);
        InitGitCommand = new RelayCommand(async p => await InitGitAsync(p as SkillRowViewModel).ConfigureAwait(true), CanPromptInitGit);
        SyncFromSourceCommand = new RelayCommand(async p => await SyncRoutingAsync(p as SkillRowViewModel, fromSource: true).ConfigureAwait(true), p => CanSkillRow(p, row => row.CanSyncFromSource));
        SyncToSourceCommand = new RelayCommand(async p => await SyncRoutingAsync(p as SkillRowViewModel, fromSource: false).ConfigureAwait(true), p => CanSkillRow(p, row => row.CanSyncToSource));
        NasLoginCommand = new RelayCommand(async p => await NasLoginAsync(p as string).ConfigureAwait(true), _ => !Busy);
        NasLogoutCommand = new RelayCommand(_ => NasLogout(), _ => GitSsh.HasLogin);
        ConfirmCommitCommand = new RelayCommand(async _ => await ConfirmCommitAsync().ConfigureAwait(true), _ => CanConfirmCommit);
        CloseCommitCommand = new RelayCommand(_ => IsCommitOpen = false);
        ConfirmGitActionCommand = new RelayCommand(async _ => await ConfirmGitActionAsync().ConfigureAwait(true), _ => _gitActionRow is not null);
        CloseGitActionCommand = new RelayCommand(_ => IsGitActionOpen = false);
        ConfirmInitGitCommand = new RelayCommand(async _ => await ConfirmInitGitAsync().ConfigureAwait(true), _ => CanConfirmInitGit);
        CloseInitGitCommand = new RelayCommand(_ => CloseInitGit(), _ => InitGitCanDismiss);
        ConfirmBranchSwitchCommand = new RelayCommand(async _ => await ConfirmBranchSwitchAsync().ConfigureAwait(true));
        CloseBranchSwitchCommand = new RelayCommand(_ => IsBranchSwitchOpen = false);
        UninstallCommand = new RelayCommand(async p => await UninstallAsync(p as SkillRowViewModel).ConfigureAwait(true), CanMutate);
        OpenInstallFolderCommand = new RelayCommand(
            p => OpenPath(p switch
            {
                SkillRowViewModel row => row.InstallFolder,
                string folder => folder,
                _ => null
            }),
            _ => HasOpenWorkspace);
        CloseConflictCommand = new RelayCommand(_ => IsConflictOpen = false);
        OpenConflictFolderCommand = new RelayCommand(_ => OpenPath(ConflictFolder), _ =>
            !string.IsNullOrWhiteSpace(ConflictFolder));
        SelectThemeCommand = new RelayCommand(p => SelectTheme(p as ThemeOptionViewModel));
        SelectSettingsTabCommand = new RelayCommand(p => SelectSettingsTab(p as SettingsTabViewModel));
        _showRepoLinks = _settings.ShowRepoLinks;
        _autoCheckUpdates = _settings.AutoCheckUpdates;
        _allowGitPush = _settings.AllowGitPush;
        _allowGitCommit = _settings.AllowGitCommit;
        _nasUser = string.IsNullOrWhiteSpace(_settings.NasUser) ? Environment.UserName : _settings.NasUser;
        ApplyGitWriteFlags();
        OpenSettingsCommand = new RelayCommand(_ => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(_ => IsSettingsOpen = false);
        OpenLogCommand = new RelayCommand(_ =>
        {
            SelectSettingsTab(SettingsTabs.First(item => item.Id == "log"));
            IsSettingsOpen = true;
        });
        CheckUpdatesCommand = new RelayCommand(async _ => await CheckUpdatesAsync(quiet: false).ConfigureAwait(true), _ => CanCheckAppUpdate);
        InstallAppUpdateCommand = new RelayCommand(async _ => await InstallAppUpdateAsync().ConfigureAwait(true), _ => CanInstallAppUpdate);
        OpenLatestReleaseCommand = new RelayCommand(
            _ => OpenPath(_latestRelease?.HtmlUrl ?? UpdateService.ReleasesUrl),
            _ => !IsDev);
        CloseLogCommand = new RelayCommand(_ => IsLogOpen = false);
        ClearLogCommand = new RelayCommand(_ =>
        {
            LogText = "";
            Log("已清空日志");
        });
        CopyLogCommand = new RelayCommand(_ =>
        {
            if (!string.IsNullOrWhiteSpace(LogText))
            {
                Clipboard.SetText(LogText);
            }
        });
        OpenConfigFolderCommand = new RelayCommand(_ => OpenPath(_paths.ConfigDirectory, create: true));
        OpenCatalogCommand = new RelayCommand(_ => OpenPath(_paths.BundledCatalogPath));
        OpenOverrideCommand = new RelayCommand(_ => OpenOverrideFile());
        OpenCacheFolderCommand = new RelayCommand(_ => OpenPath(_paths.CacheDirectory, create: true));
        OpenWorkspaceFolderCommand = new RelayCommand(_ => OpenPath(WorkspacePath), _ => HasOpenWorkspace);
        OpenProductRepoCommand = new RelayCommand(_ => OpenPath(ProductRepo));
        ClearCacheCommand = new RelayCommand(_ => ClearRepoCache());
        RestartDevCommand = new RelayCommand(_ => (Application.Current as App)?.RestartDev(), _ => ShowDevRestart);
        CopyDiagnosticsCommand = new RelayCommand(_ => Clipboard.SetText(BuildDiagnostics()));
        _scan = new SkillCatalogScan(
            AllSkills, _gh, _git, _skillGit,
            () => NasUser, () => WorkspacePath, () => HasOpenWorkspace, Log,
            ApplyAccount, ApplyNoLanCatalog, ApplyLanProbe, ApplyNasAccess,
            () =>
            {
                RaiseSkillGitCommands();
            });
        ReloadWorkspaceItems();
        if (!string.IsNullOrWhiteSpace(WorkspacePath) && Directory.Exists(WorkspacePath))
        {
            ApplyWorkspace(WorkspacePath, persist: false, autoSelectDetected: false, inspectGit: false);
        }
        else
        {
            ResetRootDefaults(Array.Empty<SkillRootInfo>(), CurrentSavedRoots(), autoSelectDetected: true);
            RefreshInstallStatuses(inspectGit: false);
        }
    }

    public ObservableCollection<RootOptionViewModel> Roots { get; } = [];
    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = [];
    public ObservableCollection<SettingsTabViewModel> SettingsTabs { get; } = [];
    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];
    public ObservableCollection<SkillRowViewModel> AllSkills { get; } = [];
    public ObservableCollection<SkillRowViewModel> VisibleSkills { get; } = [];
    public CatalogFilter CatalogFilter { get; } = new();
    public RelayCommand BrowseCommand { get; }
    public RelayCommand SelectWorkspaceCommand { get; }
    public RelayCommand RemoveWorkspaceCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand InstallCommand { get; }
    public RelayCommand PullCommand { get; }
    public RelayCommand PushCommand { get; }
    public RelayCommand CommitCommand { get; }
    public RelayCommand InitGitCommand { get; }
    public RelayCommand SyncFromSourceCommand { get; }
    public RelayCommand SyncToSourceCommand { get; }
    public RelayCommand NasLoginCommand { get; }
    public RelayCommand NasLogoutCommand { get; }
    public RelayCommand ConfirmCommitCommand { get; }
    public RelayCommand CloseCommitCommand { get; }
    public RelayCommand ConfirmGitActionCommand { get; }
    public RelayCommand CloseGitActionCommand { get; }
    public RelayCommand ConfirmInitGitCommand { get; }
    public RelayCommand CloseInitGitCommand { get; }
    public RelayCommand ConfirmBranchSwitchCommand { get; }
    public RelayCommand CloseBranchSwitchCommand { get; }
    public RelayCommand UninstallCommand { get; }
    public RelayCommand SelectThemeCommand { get; }
    public RelayCommand SelectSettingsTabCommand { get; }
    public RelayCommand CheckUpdatesCommand { get; }
    public RelayCommand InstallAppUpdateCommand { get; }
    public RelayCommand OpenLatestReleaseCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CloseSettingsCommand { get; }
    public RelayCommand OpenLogCommand { get; }
    public RelayCommand CloseLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand OpenConfigFolderCommand { get; }
    public RelayCommand OpenCatalogCommand { get; }
    public RelayCommand OpenOverrideCommand { get; }
    public RelayCommand OpenCacheFolderCommand { get; }
    public RelayCommand OpenWorkspaceFolderCommand { get; }
    public RelayCommand OpenProductRepoCommand { get; }
    public RelayCommand ClearCacheCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }
    public RelayCommand RestartDevCommand { get; }
    public RelayCommand OpenInstallFolderCommand { get; }
    public RelayCommand CloseConflictCommand { get; }
    public RelayCommand OpenConflictFolderCommand { get; }
    public string ProductRepo { get; } = "https://github.com/MlsMoon/moon-game-dev-tool-manager";
    public string ConfigPath => _paths.ConfigDirectory;
    public string CachePath => _paths.CacheDirectory;
    public string OverridePath => _paths.UserOverridePath;
    public string IgpStatus
    {
        get => _igpStatus;
        set => SetProperty(ref _igpStatus, value);
    }

    public string NasStatus
    {
        get => _nasStatus;
        set => SetProperty(ref _nasStatus, value);
    }

    public BadgeAppearance GhTone => SkillRowViewModel.ToneFor(_ghState);
    public BadgeAppearance IgpTone => SkillRowViewModel.ToneFor(_igpState);
    public BadgeAppearance NasTone => SkillRowViewModel.ToneFor(_nasState);
    public BadgeAppearance NasLoginTone => NasLoggedIn ? BadgeAppearance.Success : BadgeAppearance.Warning;

    public string SettingsTab
    {
        get => _settingsTab;
        set
        {
            if (SetProperty(ref _settingsTab, value))
            {
                Raise(nameof(IsSettingsAppearance));
                Raise(nameof(IsSettingsConnection));
                Raise(nameof(IsSettingsUpdates));
                Raise(nameof(IsSettingsFolders));
                Raise(nameof(IsSettingsAbout));
                Raise(nameof(IsSettingsLog));
            }
        }
    }

    public bool IsSettingsAppearance => SettingsTab == "appearance";
    public bool IsSettingsConnection => SettingsTab == "connection";
    public bool IsSettingsUpdates => SettingsTab == "updates";
    public bool IsSettingsFolders => SettingsTab == "folders";
    public bool IsSettingsAbout => SettingsTab == "about";
    public bool IsSettingsLog => SettingsTab == "log";

    public bool AutoCheckUpdates
    {
        get => _autoCheckUpdates;
        set
        {
            if (SetProperty(ref _autoCheckUpdates, value))
            {
                _settings.AutoCheckUpdates = value;
                SaveSettings();
            }
        }
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    public bool HasAppUpdate
    {
        get => _hasAppUpdate;
        set => SetProperty(ref _hasAppUpdate, value);
    }

    public bool UpdateBusy
    {
        get => _updateBusy;
        set
        {
            if (SetProperty(ref _updateBusy, value))
            {
                Raise(nameof(CanCheckAppUpdate));
                Raise(nameof(CanInstallAppUpdate));
                CheckUpdatesCommand?.RaiseCanExecuteChanged();
                InstallAppUpdateCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanCheckAppUpdate => !IsDev && !UpdateBusy;

    public bool CanInstallAppUpdate =>
        CanCheckAppUpdate && HasAppUpdate && _latestRelease?.HasSetup == true;

    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => SetProperty(ref _isDownloadingUpdate, value);
    }

    public double UpdateProgress
    {
        get => _updateProgress;
        set => SetProperty(ref _updateProgress, value);
    }

    public string UpdateProgressText
    {
        get => _updateProgressText;
        set => SetProperty(ref _updateProgressText, value);
    }

    public string NasUser
    {
        get => _nasUser;
        set
        {
            if (SetProperty(ref _nasUser, value.Trim()))
            {
                _settings.NasUser = _nasUser;
                SaveSettings();
                Raise(nameof(NasLoginStatus));
            }
        }
    }

    public bool NasLoggedIn => GitSsh.HasLogin;

    public string NasLoginStatus => NasLoggedIn
        ? $"已用 {NasUser} 登录。安装 / Pull / Push 会自动带上这份 SSH 凭据。"
        : "未登录。局域网仓库若要密码，先在下面登录，不要只填 Windows 用户名。";

    public bool AllowGitPush
    {
        get => _allowGitPush;
        set
        {
            if (SetProperty(ref _allowGitPush, value))
            {
                _settings.AllowGitPush = value;
                SaveSettings();
                ApplyGitWriteFlags();
            }
        }
    }

    public bool AllowGitCommit
    {
        get => _allowGitCommit;
        set
        {
            if (SetProperty(ref _allowGitCommit, value))
            {
                _settings.AllowGitCommit = value;
                SaveSettings();
                ApplyGitWriteFlags();
            }
        }
    }

    public bool IsCommitOpen
    {
        get => _isCommitOpen;
        set
        {
            if (!SetProperty(ref _isCommitOpen, value) || value)
            {
                return;
            }

            _commitRow = null;
            CommitMessage = "";
            CommitChangesText = "";
            ConfirmCommitCommand?.RaiseCanExecuteChanged();
        }
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value))
            {
                ConfirmCommitCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string CommitChangesText
    {
        get => _commitChangesText;
        set => SetProperty(ref _commitChangesText, value);
    }

    public bool CanConfirmCommit =>
        _commitRow is not null && !string.IsNullOrWhiteSpace(CommitMessage);

    public bool IsGitActionOpen
    {
        get => _isGitActionOpen;
        set
        {
            if (!SetProperty(ref _isGitActionOpen, value) || value)
            {
                return;
            }

            _gitActionRow = null;
            ConfirmGitActionCommand?.RaiseCanExecuteChanged();
        }
    }

    public string GitActionTitle
    {
        get => _gitActionTitle;
        set => SetProperty(ref _gitActionTitle, value);
    }

    public string GitActionText
    {
        get => _gitActionText;
        set => SetProperty(ref _gitActionText, value);
    }

    public string GitActionPrimaryText
    {
        get => _gitActionPrimaryText;
        set => SetProperty(ref _gitActionPrimaryText, value);
    }

    public bool IsInitGitOpen
    {
        get => _isInitGitOpen;
        set
        {
            if (!value && _initGitRunning)
            {
                return;
            }

            if (!SetProperty(ref _isInitGitOpen, value) || value)
            {
                return;
            }

            _initGitRow = null;
            InitGitProgress = 0;
            InitGitProgressText = "";
            ConfirmInitGitCommand?.RaiseCanExecuteChanged();
        }
    }

    public bool InitGitRunning
    {
        get => _initGitRunning;
        private set
        {
            if (!SetProperty(ref _initGitRunning, value))
            {
                return;
            }

            Raise(nameof(InitGitCanDismiss));
            Raise(nameof(CanConfirmInitGit));
            ConfirmInitGitCommand?.RaiseCanExecuteChanged();
            CloseInitGitCommand?.RaiseCanExecuteChanged();
        }
    }

    public bool InitGitCanDismiss => !InitGitRunning;

    public string InitGitText
    {
        get => _initGitText;
        set => SetProperty(ref _initGitText, value);
    }

    public double InitGitProgress
    {
        get => _initGitProgress;
        set => SetProperty(ref _initGitProgress, value);
    }

    public string InitGitProgressText
    {
        get => _initGitProgressText;
        set => SetProperty(ref _initGitProgressText, value);
    }

    public bool CanConfirmInitGit =>
        !InitGitRunning && _initGitRow is not null && HasOpenWorkspace;

    public bool IsBranchSwitchOpen
    {
        get => _isBranchSwitchOpen;
        set
        {
            if (!SetProperty(ref _isBranchSwitchOpen, value) || value)
            {
                return;
            }

            if (!_branchSwitchConfirmed)
            {
                _branchSwitchRow?.RevertBranchToInstalled();
            }

            _branchSwitchRow = null;
            _branchSwitchConfirmed = false;
        }
    }

    public string BranchSwitchText
    {
        get => _branchSwitchText;
        set => SetProperty(ref _branchSwitchText, value);
    }

    public bool HasWorkspaces => Workspaces.Count > 0;
    public bool HasOpenWorkspace =>
        !string.IsNullOrWhiteSpace(WorkspacePath) && Directory.Exists(WorkspacePath);
    public bool IsBrowseOnly => !HasOpenWorkspace;
    public string WorkspaceDisplay =>
        string.IsNullOrWhiteSpace(WorkspacePath) ? "未选择工作区" : WorkspacePath;
    public string BrowseOnlyHint =>
        string.IsNullOrWhiteSpace(WorkspacePath)
            ? "先添加并打开一个工作区。现在只能浏览清单，不能安装、更新或卸载。"
            : HasOpenWorkspace
                ? ""
                : "当前工作区路径不存在。现在只能浏览清单，不能安装、更新或卸载。";
    public string Version => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    public bool IsDev { get; } = Application.Current is App app && app.IsDev;
    public bool ShowDevRestart => IsDev && Application.Current is App { IsUiTest: false };
    public string WindowTitle => Application.Current is App { IsUiTest: true }
        ? "Moon Game Dev Tool Manager — UI TEST"
        : IsDev ? "Moon Game Dev Tool Manager — DEV" : "Moon Game Dev Tool Manager";

    public string WorkspacePath
    {
        get => _workspacePath;
        set
        {
            if (SetProperty(ref _workspacePath, value))
            {
                NotifyWorkspaceState();
            }
        }
    }

    public string GhStatus
    {
        get => _ghStatus;
        set => SetProperty(ref _ghStatus, value);
    }

    public string LogText
    {
        get => _logText;
        set
        {
            if (SetProperty(ref _logText, value))
            {
                Raise(nameof(LastLogLine));
            }
        }
    }

    public string LastLogLine
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogText))
            {
                return "暂无日志";
            }

            var index = LogText.LastIndexOf('\n');
            return index < 0 ? LogText : LogText[(index + 1)..];
        }
    }

    public bool IsLogOpen
    {
        get => _isLogOpen;
        set => SetProperty(ref _isLogOpen, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public bool IsConflictOpen
    {
        get => _isConflictOpen;
        set => SetProperty(ref _isConflictOpen, value);
    }

    public string ConflictText
    {
        get => _conflictText;
        set => SetProperty(ref _conflictText, value);
    }

    public string ConflictFolder
    {
        get => _conflictFolder;
        set => SetProperty(ref _conflictFolder, value);
    }

    public bool ShowRepoLinks
    {
        get => _showRepoLinks;
        set
        {
            if (SetProperty(ref _showRepoLinks, value))
            {
                _settings.ShowRepoLinks = value;
                SaveSettings();
            }
        }
    }

    public bool Busy
    {
        get => _busy;
        set
        {
            if (SetProperty(ref _busy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                InstallCommand.RaiseCanExecuteChanged();
                RaiseSkillGitCommands();
                UninstallCommand.RaiseCanExecuteChanged();
                NasLoginCommand?.RaiseCanExecuteChanged();
                ConfirmCommitCommand?.RaiseCanExecuteChanged();
                Raise(nameof(CanConfirmCommit));
            }
        }
    }

    public void OnRootsChanged()
    {
        if (_suppressRootEvents)
        {
            return;
        }

        RememberCurrentRoots();
        SaveSettings();
        RefreshInstallStatuses();
    }

    public void SelectTheme(ThemeOptionViewModel? option)
    {
        if (option is null)
        {
            return;
        }

        foreach (var theme in Themes)
        {
            theme.IsSelected = theme.Id == option.Id;
        }

        _settings.Theme = option.Id;
        ThemeManager.Apply(option.Id);
        SaveSettings();
        Log($"界面主题: {option.Label}");
    }

    public void AddWorkspace()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "添加工作区",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        RememberCurrentRoots();
        var firstVisit = WorkspaceBook.Find(_settings, dialog.FolderName) is null;
        WorkspaceBook.AddOrGet(_settings, dialog.FolderName);
        ApplyWorkspace(dialog.FolderName, persist: true, autoSelectDetected: firstVisit);
    }

    public void SelectWorkspace(WorkspaceItemViewModel? item)
    {
        if (item is null || WorkspaceBook.PathsEqual(item.Path, WorkspacePath))
        {
            return;
        }

        RememberCurrentRoots();
        ApplyWorkspace(item.Path, persist: true, autoSelectDetected: false);
    }

    public void RemoveWorkspace(WorkspaceItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var removingCurrent = WorkspaceBook.PathsEqual(item.Path, WorkspacePath);
        if (!WorkspaceBook.Remove(_settings, item.Path))
        {
            return;
        }

        Log($"已从列表移除工作区 {item.Name}");
        if (removingCurrent)
        {
            var next = _settings.Workspaces.FirstOrDefault();
            if (next is null)
            {
                WorkspacePath = "";
                ResetRootDefaults(Array.Empty<SkillRootInfo>(), [SkillRoots.DefaultRoot], autoSelectDetected: true);
                RefreshInstallStatuses();
            }
            else
            {
                ApplyWorkspace(next.Path, persist: false, autoSelectDetected: false);
            }
        }

        ReloadWorkspaceItems();
        SaveSettings();
    }

    public void ApplyWorkspace(string path, bool persist, bool autoSelectDetected, bool inspectGit = true)
    {
        var entry = WorkspaceBook.AddOrGet(_settings, path);
        WorkspacePath = entry.Path;
        if (!Directory.Exists(entry.Path))
        {
            ResetRootDefaults(Array.Empty<SkillRootInfo>(), entry.SelectedRoots, autoSelectDetected: false);
            RefreshInstallStatuses(inspectGit);
            ReloadWorkspaceItems();
            Log($"工作区不存在: {entry.Path}");
            if (persist)
            {
                SaveSettings();
            }

            return;
        }

        var info = _scanner.Scan(entry.Path);
        ResetRootDefaults(info.Roots, entry.SelectedRoots, autoSelectDetected);
        RefreshInstallStatuses(inspectGit);
        ReloadWorkspaceItems();
        Log($"工作区: {info.Path}");
        foreach (var root in info.Roots)
        {
            Log($"  {root.Name}: {(root.Exists ? "已检测到" : "不存在")}");
        }

        if (persist)
        {
            SaveSettings();
        }
    }

    public async Task RefreshAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        try
        {
            _skillGit.ClearCaches();
            await ScanCatalogAsync().ConfigureAwait(true);
        }
        finally
        {
            Busy = false;
        }

        if (!IsDev && AutoCheckUpdates)
        {
            await CheckUpdatesAsync(quiet: true).ConfigureAwait(true);
        }
    }

    private async Task InstallAsync(SkillRowViewModel? row)
    {
        if (row is null || !TryBeginMutation(row, requireAccess: true, out var roots))
        {
            return;
        }

        Busy = true;
        try
        {
            if (row.IsCompanion)
            {
                Log(row.Definition.IsRouting
                    ? $"{row.Name} 是路由 Skill，请安装所属 Plugin。"
                    : $"{row.Name} 是随附 Skill，请安装所属 Plugin，不能单独当 Skill 安装。");
                return;
            }

            if (SkillGit.IsForbiddenBranch(
                    row.Definition,
                    UnityWorkspace.ReadEditorVersion(WorkspacePath),
                    row.SelectedBranch))
            {
                Log($"{row.Name}: Unity 6 不能使用 master 上的 URP 14，请改选 urp-17.5。");
                return;
            }

            var branchNote = string.IsNullOrWhiteSpace(row.SelectedBranch) ? "" : $"（{row.SelectedBranch}）";
            Log($"安装 {row.Name}{branchNote}");
            await _installer.InstallAsync(row.Definition, WorkspacePath, roots, Log, NasUser, row.SelectedBranch)
                .ConfigureAwait(true);
            _skillGit.ClearCaches();
            Log($"完成 {row.Name}");
            RefreshInstallStatuses();
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
        }
        finally
        {
            Busy = false;
        }
    }

    private bool CanPromptInitGit(object? parameter)
    {
        return parameter is not SkillRowViewModel row || row.CanInitGit;
    }

    private Task InitGitAsync(SkillRowViewModel? row)
    {
        if (row is null)
        {
            Log("初始化 Git 时没有拿到卡片。");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath))
        {
            Log("请先选择有效工作区。");
            return Task.CompletedTask;
        }

        if (!row.Access.CanInstall)
        {
            Log($"{row.Name}: 当前无权限访问");
            return Task.CompletedTask;
        }

        if (row.Git.State != SkillGitState.NeedsAttach)
        {
            Log($"{row.Name}: 已经接上 Git。");
            return Task.CompletedTask;
        }

        if (SkillGit.IsForbiddenBranch(
                row.Definition,
                UnityWorkspace.ReadEditorVersion(WorkspacePath),
                row.SelectedBranch))
        {
            Log($"{row.Name}: {row.Git.Warning}");
            return Task.CompletedTask;
        }

        PromptInitGit(row);
        return Task.CompletedTask;
    }

    private void PromptInitGit(SkillRowViewModel row)
    {
        var branch = InitGitBranch(row);
        var branchNote = string.IsNullOrWhiteSpace(branch) ? "所选分支" : "origin/" + branch;
        var match = row.Git.TreeMatchesRemote
            ? $"当前文件树已经和 {branchNote} 一样；初始化仍会 reset --hard 接到该分支。"
            : $"当前文件和 {branchNote} 不完全一样。初始化会按远端覆盖同名文件。";
        _initGitRow = row;
        InitGitProgress = 0;
        InitGitProgressText = "确认后开始初始化。";
        InitGitText =
            $"{row.Name} 会把安装目录接到 {branchNote}。\n\n{match}\n未跟踪文件（例如 .mlsmoon）会留下。不能从本工具一键撤销。";
        IsInitGitOpen = true;
        ConfirmInitGitCommand?.RaiseCanExecuteChanged();
        Log($"{row.Name}: 等待确认初始化 Git。");
    }

    private void CloseInitGit()
    {
        if (_initGitRunning)
        {
            return;
        }

        IsInitGitOpen = false;
    }

    private async Task ConfirmInitGitAsync()
    {
        var row = _initGitRow;
        if (row is null || _initGitRunning)
        {
            return;
        }

        await AdoptRemoteForRowAsync(row).ConfigureAwait(true);
    }

    private static string InitGitBranch(SkillRowViewModel row)
    {
        return string.IsNullOrWhiteSpace(row.SelectedBranch) ? row.Git.TargetBranch : row.SelectedBranch;
    }

    private async Task AdoptRemoteForRowAsync(SkillRowViewModel row)
    {
        var dest = row.InstallFolder;
        if (string.IsNullOrWhiteSpace(dest) || !WorkspaceRepo.CanAttach(row.Definition))
        {
            Log($"{row.Name} 不能把安装目录接到远端。");
            return;
        }

        var origin = WorkspaceRepo.OriginUrl(row.Definition, NasUser);
        if (string.IsNullOrWhiteSpace(origin))
        {
            Log($"{row.Name}: 没有远端地址。");
            return;
        }

        var branch = InitGitBranch(row);
        if (string.IsNullOrWhiteSpace(branch))
        {
            Log($"{row.Name}: 没有分支，无法初始化 Git。");
            return;
        }

        InitGitRunning = true;
        ReportInitGit("正在初始化 Git…", 8);
        row.MarkGitChecking();
        row.SetScan("正在初始化 Git…", 8);
        Busy = true;
        try
        {
            Log($"初始化 Git {row.Name}（{branch}）");
            var progress = new Progress<ScanProgress>(step =>
            {
                ReportInitGit(step.Text, step.Percent);
                row.SetScan(step.Text, step.Percent);
            });
            await _workspaceRepo.AdoptRemoteAsync(dest, origin, branch, Log, progress).ConfigureAwait(true);
            ReportInitGit("初始化完成", 100);
            row.SetScan("初始化完成", 100);
            _skillGit.ClearCaches();
            Log($"完成 {row.Name}");
            InitGitRunning = false;
            IsInitGitOpen = false;
            RefreshInstallStatuses(inspectGit: false);
            await _scan.InspectOneAsync(row).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportInitGit("初始化失败：" + ex.Message, (int)InitGitProgress);
            Log($"失败: {ex.Message}");
            try
            {
                await _scan.InspectOneAsync(row).ConfigureAwait(true);
            }
            catch (Exception inspectEx)
            {
                Log($"对照失败: {inspectEx.Message}");
            }
        }
        finally
        {
            InitGitRunning = false;
            Busy = false;
        }
    }

    private void ReportInitGit(string text, int percent)
    {
        InitGitProgress = percent;
        InitGitProgressText = text;
    }

    private Task PullAsync(SkillRowViewModel? row)
    {
        if (row is null)
        {
            Log("Pull 时没有拿到卡片。");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath))
        {
            Log("请先选择有效工作区。");
            return Task.CompletedTask;
        }

        if (!row.Access.CanInstall)
        {
            Log($"{row.Name}: 当前无权限访问");
            return Task.CompletedTask;
        }

        if (SkillGit.IsForbiddenBranch(
                row.Definition,
                UnityWorkspace.ReadEditorVersion(WorkspacePath),
                row.SelectedBranch))
        {
            Log($"{row.Name}: Unity 6 不能使用 master 上的 URP 14，请改选 urp-17.5。");
            return Task.CompletedTask;
        }

        if (NeedsBranchSwitch(row))
        {
            PromptBranchSwitch(row);
            return Task.CompletedTask;
        }

        if (!CanAutoUpdate(row))
        {
            return Task.CompletedTask;
        }

        var branch = string.IsNullOrWhiteSpace(row.SelectedBranch) ? row.Git.InstalledBranch : row.SelectedBranch;
        PromptGitAction(
            GitCardAction.Pull,
            row,
            "确认 Pull",
            SkillGitPresentation.PullConfirm(row.Name, branch, row.Git.Compare.BehindBy),
            "开始 Pull");
        return Task.CompletedTask;
    }

    private Task PushAsync(SkillRowViewModel? row)
    {
        if (row is null)
        {
            Log("Push 时没有拿到卡片。");
            return Task.CompletedTask;
        }

        if (!AllowGitPush)
        {
            Log("请在设置「连接」里打开「允许 Push」。");
            return Task.CompletedTask;
        }

        if (row.Git.Forbidden)
        {
            Log($"{row.Name}: {row.Git.Warning}");
            return Task.CompletedTask;
        }

        var dest = row.InstallFolder;
        if (string.IsNullOrWhiteSpace(dest) || !WorkspaceGit.HasRepo(dest))
        {
            Log($"{row.Name} 安装目录没有 .git，先安装或 Pull 接上远端。");
            return Task.CompletedTask;
        }

        if (row.Git.State != SkillGitState.Ahead)
        {
            Log($"{row.Name}: 没有可推送的本地提交。有未提交改动请先 Commit。");
            return Task.CompletedTask;
        }

        var branch = string.IsNullOrWhiteSpace(row.SelectedBranch) ? row.Git.InstalledBranch : row.SelectedBranch;
        PromptGitAction(
            GitCardAction.Push,
            row,
            "确认 Push",
            SkillGitPresentation.PushConfirm(row.Name, branch, row.Git.Compare.AheadBy),
            "开始 Push");
        return Task.CompletedTask;
    }

    private Task CommitAsync(SkillRowViewModel? row)
    {
        if (row is null)
        {
            Log("Commit 时没有拿到卡片。");
            return Task.CompletedTask;
        }

        if (!AllowGitCommit)
        {
            Log("请在设置「连接」里打开「允许 Commit」。");
            return Task.CompletedTask;
        }

        if (!row.CanCommit)
        {
            Log($"{row.Name}: 没有可提交的本地改动。");
            return Task.CompletedTask;
        }

        _commitRow = row;
        CommitMessage = "";
        CommitChangesText = row.LocalChangesText;
        IsCommitOpen = true;
        ConfirmCommitCommand?.RaiseCanExecuteChanged();
        Log($"{row.Name}: 等待填写提交说明。");
        return Task.CompletedTask;
    }

    private void PromptGitAction(
        GitCardAction action,
        SkillRowViewModel row,
        string title,
        string text,
        string primary)
    {
        _gitAction = action;
        _gitActionRow = row;
        GitActionTitle = title;
        GitActionText = text;
        GitActionPrimaryText = primary;
        IsGitActionOpen = true;
        ConfirmGitActionCommand?.RaiseCanExecuteChanged();
        Log($"{row.Name}: 等待确认{title}。");
    }

    private async Task ConfirmGitActionAsync()
    {
        var row = _gitActionRow;
        var action = _gitAction;
        IsGitActionOpen = false;
        if (row is null)
        {
            return;
        }

        if (action == GitCardAction.Pull)
        {
            await ExecutePullAsync(row).ConfigureAwait(true);
            return;
        }

        var dest = row.InstallFolder;
        if (string.IsNullOrWhiteSpace(dest))
        {
            Log($"{row.Name} 安装目录不存在。");
            return;
        }

        await PushExistingAsync(row, dest).ConfigureAwait(true);
    }

    private async Task ExecutePullAsync(SkillRowViewModel row)
    {
        if (!TryBeginMutation(row, requireAccess: true, out var roots))
        {
            return;
        }

        Busy = true;
        try
        {
            var branchNote = string.IsNullOrWhiteSpace(row.SelectedBranch) ? "" : $"（{row.SelectedBranch}）";
            Log($"Pull {row.Name}{branchNote}");
            var dest = row.InstallFolder;
            if (WorkspaceRepo.CanAttach(row.Definition)
                && !string.IsNullOrWhiteSpace(dest)
                && WorkspaceGit.HasRepo(dest))
            {
                var origin = WorkspaceRepo.OriginUrl(row.Definition, NasUser)
                             ?? throw new InvalidOperationException("没有远端地址。");
                await _workspaceRepo.EnsureAttachedAsync(dest, origin, row.SelectedBranch, Log)
                    .ConfigureAwait(true);
                await _workspaceRepo.FastForwardAsync(dest, row.SelectedBranch).ConfigureAwait(true);
            }
            else
            {
                await _installer.InstallAsync(row.Definition, WorkspacePath, roots, Log, NasUser, row.SelectedBranch)
                    .ConfigureAwait(true);
            }

            _skillGit.ClearCaches();
            Log($"完成 {row.Name}");
            RefreshInstallStatuses(inspectGit: false);
            await _scan.InspectOneAsync(row).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task ConfirmCommitAsync()
    {
        var row = _commitRow;
        if (row is null || string.IsNullOrWhiteSpace(CommitMessage))
        {
            Log("请填写提交说明。");
            return;
        }

        var dest = row.InstallFolder;
        if (string.IsNullOrWhiteSpace(dest) || !WorkspaceGit.HasRepo(dest))
        {
            Log($"{row.Name} 安装目录没有 .git，无法提交。");
            return;
        }

        Busy = true;
        try
        {
            Log($"Commit {row.Name}");
            await _workspaceRepo.CommitAsync(dest, CommitMessage).ConfigureAwait(true);
            IsCommitOpen = false;
            _skillGit.ClearCaches();
            Log($"完成 {row.Name}：已提交，未 Push。");
            RefreshInstallStatuses(inspectGit: false);
            await _scan.InspectOneAsync(row).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task PushExistingAsync(SkillRowViewModel row, string dest, bool alreadyBusy = false)
    {
        if (!alreadyBusy)
        {
            Busy = true;
        }

        try
        {
            var branch = string.IsNullOrWhiteSpace(row.SelectedBranch) ? row.Git.InstalledBranch : row.SelectedBranch;
            Log($"Push {row.Name}（{branch}）");
            var origin = WorkspaceRepo.OriginUrl(row.Definition, NasUser);
            if (!string.IsNullOrWhiteSpace(origin))
            {
                await _workspaceRepo.EnsureAttachedAsync(dest, origin, branch, Log, fetch: false)
                    .ConfigureAwait(true);
            }

            await _workspaceRepo.PushAsync(dest, branch).ConfigureAwait(true);
            _skillGit.ClearCaches();
            Log($"完成 {row.Name}");
            RefreshInstallStatuses();
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task NasLoginAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(NasUser))
        {
            Log("请填写 NAS SSH 用户名（ssh:// 后面 @ 前面那段，不是 Windows 用户名）。");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            Log("请填写 NAS SSH 密码。");
            return;
        }

        Busy = true;
        try
        {
            NasCredentials.Save(NasUser, password);
            var lan = AllSkills.Select(item => item.Definition).FirstOrDefault(item => item.IsLan);
            if (lan is null)
            {
                Log($"已保存 NAS 用户 {NasUser}。清单里还没有局域网条目，无法当场验证。");
                NotifyNasLogin();
                return;
            }

            var access = await _git.CheckLanAccessAsync(lan, NasUser).ConfigureAwait(true);
            if (access.State != AccessState.Accessible)
            {
                NasCredentials.Delete();
                GitSsh.ClearAskpass();
                Log("NAS 登录失败: " + access.Message);
                NotifyNasLogin();
                return;
            }

            Log($"NAS 已登录（{NasUser}）。之后的安装 / Pull / Push 会自动带上这份凭据。");
            NotifyNasLogin();
            _skillGit.ClearCaches();
            await ScanCatalogAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            NasCredentials.Delete();
            GitSsh.ClearAskpass();
            Log("NAS 登录失败: " + ex.Message);
            NotifyNasLogin();
        }
        finally
        {
            Busy = false;
        }
    }

    private void NasLogout()
    {
        NasCredentials.Delete();
        GitSsh.ClearAskpass();
        Log("已退出 NAS 登录。");
        NotifyNasLogin();
        _ = RefreshAsync();
    }

    private void NotifyNasLogin()
    {
        Raise(nameof(NasLoggedIn));
        Raise(nameof(NasLoginStatus));
        Raise(nameof(NasLoginTone));
        NasLogoutCommand?.RaiseCanExecuteChanged();
    }

    private void ApplyGitWriteFlags()
    {
        foreach (var row in AllSkills)
        {
            row.SetGitWriteFlags(AllowGitPush, AllowGitCommit);
        }

        RaiseSkillGitCommands();
    }

    private void RaiseSkillGitCommands()
    {
        PullCommand?.RaiseCanExecuteChanged();
        PushCommand?.RaiseCanExecuteChanged();
        CommitCommand?.RaiseCanExecuteChanged();
        InitGitCommand?.RaiseCanExecuteChanged();
        SyncFromSourceCommand?.RaiseCanExecuteChanged();
        SyncToSourceCommand?.RaiseCanExecuteChanged();
    }

    private async Task SyncRoutingAsync(SkillRowViewModel? row, bool fromSource)
    {
        var action = fromSource ? "从源同步" : "同步到源";
        if (row is null)
        {
            Log($"{action}时没有拿到卡片。");
            return;
        }

        if (!row.Definition.IsRouting)
        {
            Log($"{row.Name} 不是路由 Skill，不能{action}。");
            return;
        }

        if (!TryBeginMutation(row, requireAccess: false, out _))
        {
            return;
        }

        var parent = ParentRow(row);
        if (parent is null)
        {
            Log($"{row.Name}: 找不到所属 Plugin。");
            return;
        }

        Busy = true;
        try
        {
            row.MarkGitChecking();
            row.SetScan(fromSource ? "从源同步…" : "同步到源…", 50);
            var align = await Task.Run(() => fromSource
                    ? RoutingSkillSource.CopyFromSource(
                        parent.Definition, row.Definition, WorkspacePath, SkillRoots.DetectableRoots, null)
                    : RoutingSkillSource.CopyToSource(
                        parent.Definition, row.Definition, WorkspacePath, SkillRoots.DetectableRoots, null))
                .ConfigureAwait(true);
            Log(fromSource
                ? $"从源同步 {row.Name} ← {RoutingSkillSource.RelativeSourcePath(row.Definition.Id)}"
                : $"同步到源 {row.Name} → {RoutingSkillSource.RelativeSourcePath(row.Definition.Id)}");
            Log(align.Summary);
            row.ApplyRouting(align);
            RaiseSkillGitCommands();
            RefreshInstallStatuses(inspectGit: false);
            if (!fromSource)
            {
                await _scan.InspectOneAsync(parent).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            row.ApplyGit(SkillGitStatus.Empty);
            Log($"失败: {ex.Message}");
        }
        finally
        {
            Busy = false;
        }
    }

    private Task UninstallAsync(SkillRowViewModel? row)
    {
        if (row is null || !TryBeginMutation(row, requireAccess: false, out var roots))
        {
            return Task.CompletedTask;
        }

        try
        {
            _installer.Uninstall(row.Definition, WorkspacePath, roots, Log);
            Log($"已卸载 {row.Name}");
            RefreshInstallStatuses();
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private bool TryBeginMutation(SkillRowViewModel row, bool requireAccess, out List<string> roots)
    {
        roots = SelectedRootNames();
        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath))
        {
            Log("请先选择有效工作区。");
            return false;
        }

        if (!row.Definition.IsProjectCopy && !row.Definition.IsRouting && roots.Count == 0)
        {
            Log($"请至少勾选一个 Skill 安装目标。默认建议 {SkillRoots.DefaultRoot}。");
            return false;
        }

        if (requireAccess && !row.Access.CanInstall)
        {
            Log($"{row.Name}: 当前无权限访问");
            return false;
        }

        return true;
    }

    private bool CanMutate(object? parameter)
    {
        return CanSkillRow(parameter);
    }

    private bool CanSkillRow(object? parameter, Func<SkillRowViewModel, bool>? extra = null)
    {
        if (Busy || !HasOpenWorkspace)
        {
            return false;
        }

        return parameter is not SkillRowViewModel row || extra is null || extra(row);
    }

    private SkillRowViewModel? ParentRow(SkillRowViewModel row)
    {
        if (!string.IsNullOrWhiteSpace(row.Definition.ParentPluginId))
        {
            var parent = AllSkills.FirstOrDefault(item =>
                item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));
            if (parent is not null)
            {
                return parent;
            }
        }

        return AllSkills.FirstOrDefault(item =>
            item.Definition.IsProjectCopy
            && item.Definition.CompanionSkills.Any(companion =>
                companion.Id.Equals(row.Definition.Id, StringComparison.OrdinalIgnoreCase)));
    }

    private bool CanAutoUpdate(SkillRowViewModel row)
    {
        if (!GitUpdateGuard.TryBlock(row, out var message, out var showDialog))
        {
            return true;
        }

        if (showDialog)
        {
            ShowConflict(row, message);
        }
        else
        {
            Log(message);
        }

        return false;
    }

    private void ShowConflict(SkillRowViewModel row, string lead)
    {
        var body = string.IsNullOrWhiteSpace(row.LocalChangesText)
            ? lead
            : lead + Environment.NewLine + Environment.NewLine + row.LocalChangesText;
        ConflictText = body;
        ConflictFolder = row.InstallFolder;
        IsConflictOpen = true;
        Log($"{row.Name}: {row.Git.Message}");
    }

    private void OnOpenFolderRequested(object? sender, EventArgs e)
    {
        if (sender is SkillRowViewModel row)
        {
            OpenPath(row.InstallFolder);
        }
    }

    private void OnSelectedBranchChanged(object? sender, EventArgs e)
    {
        if (sender is not SkillRowViewModel row || !HasOpenWorkspace || row.IsRouting)
        {
            return;
        }

        if (row.IsLoading || Busy)
        {
            row.RevertBranchToInstalled();
            return;
        }

        if (!NeedsBranchSwitch(row))
        {
            _ = _scan.InspectOneAsync(row);
            return;
        }

        if (row.Git.State is SkillGitState.Conflict or SkillGitState.LocalChanges
            || row.Git.Changes.Count > 0)
        {
            ShowConflict(row, $"{row.Name} 工作区有本地修改，不能切换分支。请先处理安装目录里的改动。");
            row.RevertBranchToInstalled();
            return;
        }

        PromptBranchSwitch(row);
    }

    private static bool NeedsBranchSwitch(SkillRowViewModel row)
    {
        return row.IsInstalled
               && WorkspaceGit.HasRepo(row.InstallFolder)
               && row.Git.State != SkillGitState.NeedsAttach
               && !string.IsNullOrWhiteSpace(row.Git.InstalledBranch)
               && !string.IsNullOrWhiteSpace(row.SelectedBranch)
               && !row.SelectedBranch.Equals(row.Git.InstalledBranch, StringComparison.OrdinalIgnoreCase);
    }

    private void PromptBranchSwitch(SkillRowViewModel row)
    {
        var from = row.Git.InstalledBranch;
        var to = row.SelectedBranch;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        var editor = UnityWorkspace.ReadEditorVersion(WorkspacePath);
        _branchSwitchConfirmed = false;
        _branchSwitchRow = row;
        BranchSwitchText = SkillGitPresentation.BranchSwitchWarning(
            row.Name,
            from,
            to,
            SkillGit.IsForbiddenBranch(row.Definition, editor, to));
        IsBranchSwitchOpen = true;
    }

    private async Task ConfirmBranchSwitchAsync()
    {
        var row = _branchSwitchRow;
        var to = row?.SelectedBranch ?? "";
        _branchSwitchConfirmed = true;
        if (row is null || string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        var editor = UnityWorkspace.ReadEditorVersion(WorkspacePath);
        if (SkillGit.IsForbiddenBranch(row.Definition, editor, to))
        {
            Log($"{row.Name}: Unity 6 不能使用 master 上的 URP 14，请改选 urp-17.5。");
            row.RevertBranchToInstalled();
            return;
        }

        var dest = row.InstallFolder;
        if (string.IsNullOrWhiteSpace(dest) || !WorkspaceGit.HasRepo(dest))
        {
            Log($"{row.Name} 安装目录没有 .git，无法切换分支。");
            row.RevertBranchToInstalled();
            return;
        }

        Busy = true;
        try
        {
            Log($"切换分支 {row.Name}：{row.Git.InstalledBranch} → {to}");
            var origin = WorkspaceRepo.OriginUrl(row.Definition, NasUser)
                         ?? throw new InvalidOperationException("没有远端地址。");
            await _workspaceRepo.EnsureAttachedAsync(dest, origin, to, Log).ConfigureAwait(true);
            await _workspaceRepo.SwitchBranchAsync(dest, to, Log).ConfigureAwait(true);
            row.ClearUserBranchPick();
            _skillGit.ClearCaches();
            Log($"已切换到 {to}");
            await _scan.InspectOneAsync(row).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log($"失败: {ex.Message}");
            row.ClearUserBranchPick();
            await _scan.InspectOneAsync(row).ConfigureAwait(true);
        }
        finally
        {
            Busy = false;
        }
    }

    private void NotifyWorkspaceState()
    {
        Raise(nameof(HasOpenWorkspace));
        Raise(nameof(IsBrowseOnly));
        Raise(nameof(WorkspaceDisplay));
        Raise(nameof(BrowseOnlyHint));
        InstallCommand?.RaiseCanExecuteChanged();
        RaiseSkillGitCommands();
        UninstallCommand?.RaiseCanExecuteChanged();
        OpenWorkspaceFolderCommand?.RaiseCanExecuteChanged();
        OpenInstallFolderCommand?.RaiseCanExecuteChanged();
    }

    private void SyncRoutingRows()
    {
        var result = CatalogRouting.Bind(
            AllSkills.Select(row => row.Definition).ToList(),
            string.IsNullOrWhiteSpace(WorkspacePath) ? null : WorkspacePath,
            _paths);
        foreach (var id in result.RemoveIds)
        {
            var existing = AllSkills.FirstOrDefault(row =>
                row.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                AllSkills.Remove(existing);
            }
        }

        foreach (var skill in result.Add)
        {
            if (AllSkills.Any(row => row.Definition.Id.Equals(skill.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = new SkillRowViewModel(skill);
            row.SelectedBranchChanged += OnSelectedBranchChanged;
            row.OpenFolderRequested += OnOpenFolderRequested;
            AllSkills.Add(row);
        }

        ApplyGitWriteFlags();
    }

    private async Task ScanCatalogAsync()
    {
        RefreshInstallStatuses(inspectGit: false);
        await _scan.ScanAllAsync().ConfigureAwait(true);
    }

    private void RefreshInstallStatuses(bool inspectGit = true)
    {
        SyncRoutingRows();
        foreach (var row in AllSkills)
        {
            row.ApplyDisplay();
        }

        var roots = SkillRoots.Normalize(SelectedRootNames()).ToList();

        foreach (var row in AllSkills)
        {
            if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath))
            {
                row.Installs = [];
            }
            else
            {
                row.Installs = _scanner.InspectInstalls(WorkspacePath, row.Definition, roots);
            }

            row.RefreshInstallSummary();
        }

        ApplyFilter();
        if (inspectGit)
        {
            _ = _scan.InspectAllAsync();
        }
    }

    private List<string> SelectedRootNames()
    {
        return Roots.Where(r => r.IsSelected).Select(r => r.Name).ToList();
    }

    private void ResetRootDefaults(
        IReadOnlyList<SkillRootInfo> detected,
        IReadOnlyList<string> savedRoots,
        bool autoSelectDetected)
    {
        var existing = detected
            .Where(r => r.Exists)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var saved = SkillRoots.Normalize(savedRoots).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressRootEvents = true;
        try
        {
            foreach (var option in Roots)
            {
                var info = detected.FirstOrDefault(r => r.Name.Equals(option.Name, StringComparison.OrdinalIgnoreCase));
                option.Badge = info?.Badge ?? (option.IsDefault ? "将创建" : "未检测到");
                option.IsSelected = autoSelectDetected
                    ? option.IsDefault || existing.Contains(option.Name)
                    : saved.Contains(option.Name) || (saved.Count == 0 && option.IsDefault);
            }
        }
        finally
        {
            _suppressRootEvents = false;
        }
    }

    private void RememberCurrentRoots()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            return;
        }

        var entry = WorkspaceBook.Find(_settings, WorkspacePath);
        if (entry is null)
        {
            return;
        }

        entry.SelectedRoots = SelectedRootNames();
        _settings.SelectedRoots = entry.SelectedRoots;
    }

    private IReadOnlyList<string> CurrentSavedRoots()
    {
        return WorkspaceBook.Find(_settings, WorkspacePath)?.SelectedRoots
               ?? _settings.SelectedRoots;
    }

    private void ReloadWorkspaceItems()
    {
        Workspaces.Clear();
        foreach (var entry in _settings.Workspaces)
        {
            Workspaces.Add(new WorkspaceItemViewModel(entry)
            {
                IsSelected = WorkspaceBook.PathsEqual(entry.Path, WorkspacePath)
            });
        }

        Raise(nameof(HasWorkspaces));
    }

    private void ApplyFilter() => CatalogFilter.Fill(AllSkills, VisibleSkills);

    public void SelectSettingsTab(SettingsTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        foreach (var item in SettingsTabs)
        {
            item.IsSelected = item.Id == tab.Id;
        }

        SettingsTab = tab.Id;
    }

    private void ApplyAccount(GhAccountStatus account)
    {
        _account = account;
        GhStatus = account.Detail;
        _ghState = account.AsAccessState();
        Raise(nameof(GhTone));
    }

    private void ApplyNoLanCatalog()
    {
        IgpStatus = "未配置局域网条目。把 source: lan 写进本机 skills.override.json。";
        _igpState = AccessState.Unknown;
        _nasState = AccessState.Unknown;
        NasStatus = "没有局域网包。";
        Raise(nameof(IgpTone));
        Raise(nameof(NasTone));
    }

    private void ApplyLanProbe(LanProbe probe)
    {
        IgpStatus = probe.Detail;
        _igpState = !probe.OnLan
            ? AccessState.OffNetwork
            : probe.HostReachable
                ? AccessState.Accessible
                : AccessState.Unreachable;
        Raise(nameof(IgpTone));
        if (!probe.OnLan)
        {
            _nasState = AccessState.OffNetwork;
            NasStatus = "不在该局域网，NAS 完全不可达。";
        }
        else if (!probe.HostReachable)
        {
            _nasState = AccessState.Unreachable;
            NasStatus = "NAS 不可达（22 端口不通）。这不是缺密码，而是主机连不上。";
        }
        else
        {
            _nasState = AccessState.Checking;
            NasStatus = "NAS 网络可达，正在检查 SSH…";
        }

        Raise(nameof(NasTone));
    }

    private void ApplyNasAccess(RepoAccess access)
    {
        _nasState = access.State;
        NasStatus = string.IsNullOrWhiteSpace(access.Message) ? access.State.ToString() : access.Message;
        Raise(nameof(NasTone));
    }

    private async Task CheckUpdatesAsync(bool quiet)
    {
        if (IsDev || UpdateBusy)
        {
            return;
        }

        UpdateBusy = true;
        try
        {
            if (!quiet)
            {
                Log("正在检查 GitHub Release…");
            }

            var latest = await _updater.GetLatestAsync().ConfigureAwait(true);
            _latestRelease = latest;
            var currentText = Version;
            if (!UpdateService.TryParseTag(currentText, out var current))
            {
                current = new System.Version(0, 0, 0);
            }

            if (UpdateService.IsNewer(latest.Version, current))
            {
                HasAppUpdate = true;
                UpdateStatus = latest.HasSetup
                    ? $"有新版本 {latest.Tag}，可下载安装包。"
                    : $"有新版本 {latest.Tag}，但 Release 没有 Setup.exe。";
                Log($"发现应用更新 {latest.Tag}");
            }
            else
            {
                HasAppUpdate = false;
                UpdateStatus = $"已是最新 · {latest.Tag}";
                if (!quiet)
                {
                    Log(UpdateStatus);
                }
            }

            Raise(nameof(CanInstallAppUpdate));
            InstallAppUpdateCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            UpdateStatus = "检查更新失败: " + ex.Message;
            if (!quiet)
            {
                Log(UpdateStatus);
            }
        }
        finally
        {
            UpdateBusy = false;
        }
    }

    private async Task InstallAppUpdateAsync()
    {
        if (IsDev)
        {
            return;
        }

        if (_latestRelease is null)
        {
            await CheckUpdatesAsync(quiet: false).ConfigureAwait(true);
        }

        if (_latestRelease is null || !_latestRelease.HasSetup)
        {
            OpenPath(_latestRelease?.HtmlUrl ?? UpdateService.ReleasesUrl);
            return;
        }

        UpdateBusy = true;
        IsDownloadingUpdate = true;
        UpdateProgress = 0;
        UpdateProgressText = "准备下载…";
        try
        {
            _paths.EnsureWritable();
            Directory.CreateDirectory(_paths.UpdatesDirectory);
            var dest = Path.Combine(_paths.UpdatesDirectory, _latestRelease.SetupName);
            Log("正在下载 " + _latestRelease.SetupName);
            var progress = new Progress<DownloadProgress>(p =>
            {
                UpdateProgress = p.HasTotal ? p.Percent : 0;
                UpdateProgressText = p.HasTotal
                    ? $"{FormatBytes(p.Received)} / {FormatBytes(p.Total!.Value)}"
                    : FormatBytes(p.Received);
            });
            await _updater.DownloadAsync(_latestRelease.SetupUrl, dest, progress).ConfigureAwait(true);
            UpdateProgress = 100;
            UpdateProgressText = "正在覆盖安装…";
            Log("已下载安装包，即将退出并静默覆盖。");
            Process.Start(new ProcessStartInfo
            {
                FileName = dest,
                Arguments = UpdateService.SilentSetupArgs,
                UseShellExecute = true
            });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log("安装更新失败: " + ex.Message);
            UpdateStatus = "安装更新失败: " + ex.Message;
            IsDownloadingUpdate = false;
            UpdateBusy = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024.0).ToString("0.0") + " KB";
        }

        return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
    }

    private void SaveSettings()
    {
        RememberCurrentRoots();
        _settings.LastWorkspace = WorkspacePath;
        _settings.SelectedRoots = SelectedRootNames();
        _settings.Theme = ThemeResolver.Normalize(_settings.Theme);
        _settings.NasUser = NasUser;
        _settings.AllowGitPush = AllowGitPush;
        _settings.AllowGitCommit = AllowGitCommit;
        _settings.AutoCheckUpdates = AutoCheckUpdates;
        WorkspaceBook.Dedup(_settings);
        _settingsStore.Save(_settings);
    }

    private void OpenOverrideFile()
    {
        _paths.EnsureWritable();
        var path = _paths.EditableOverridePath;
        if (!File.Exists(path))
        {
            var example = Path.Combine(_paths.AppDirectory, "catalog", "skills.override.example.json");
            if (File.Exists(example))
            {
                File.Copy(example, path);
            }
            else
            {
                File.WriteAllText(path, """{"version":1,"skills":[],"plugins":[],"packages":[]}""" + Environment.NewLine);
            }

            Log($"已创建覆盖清单 {path}");
        }

        OpenPath(path);
    }

    private void ClearRepoCache()
    {
        try
        {
            if (!Directory.Exists(_paths.CacheDirectory))
            {
                Log("克隆缓存是空的。");
                return;
            }

            foreach (var directory in Directory.GetDirectories(_paths.CacheDirectory))
            {
                Directory.Delete(directory, true);
            }

            foreach (var file in Directory.GetFiles(_paths.CacheDirectory))
            {
                File.Delete(file);
            }

            Log("已清空克隆缓存。下次安装或更新会重新拉取。");
        }
        catch (Exception ex)
        {
            Log($"清空缓存失败: {ex.Message}");
        }
    }

    private string BuildDiagnostics()
    {
        var text = new StringBuilder();
        text.AppendLine($"Moon Game Dev Tool Manager {Version}{(IsDev ? " DEV" : "")}");
        text.AppendLine($"Theme: {_settings.Theme}");
        text.AppendLine($"Workspace: {WorkspaceDisplay}");
        text.AppendLine($"gh: {GhStatus}");
        text.AppendLine($"LAN: {IgpStatus}");
        text.AppendLine($"NAS: {NasStatus}");
        text.AppendLine($"NAS user: {NasUser}");
        text.AppendLine($"NAS login: {(NasLoggedIn ? "yes" : "no")}");
        text.AppendLine($"Git push: {(AllowGitPush ? "on" : "off")}");
        text.AppendLine($"Git commit: {(AllowGitCommit ? "on" : "off")}");
        text.AppendLine($"App update: {UpdateStatus}");
        text.AppendLine($"Config: {_paths.ConfigDirectory}");
        text.AppendLine($"Cache: {_paths.CacheDirectory}");
        return text.ToString().TrimEnd();
    }

    private void OpenPath(string? path, bool create = false) =>
        ShellFolders.Open(path, create, _paths.ConfigDirectory, Log);

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrWhiteSpace(LogText) ? line : LogText + Environment.NewLine + line;
    }

    private enum GitCardAction
    {
        Pull,
        Push
    }
}
