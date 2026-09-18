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
    private string _filter = "";
    private bool _busy;
    private bool _isLogOpen;
    private bool _isSettingsOpen;
    private bool _isConflictOpen;
    private string _conflictText = "";
    private string _conflictFolder = "";
    private bool _showRepoLinks;
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
            AllSkills.Add(row);
        }

        ApplyFilter();
        BrowseCommand = new RelayCommand(_ => AddWorkspace());
        SelectWorkspaceCommand = new RelayCommand(p => SelectWorkspace(p as WorkspaceItemViewModel));
        RemoveWorkspaceCommand = new RelayCommand(p => RemoveWorkspace(p as WorkspaceItemViewModel));
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync().ConfigureAwait(true), _ => !Busy);
        InstallCommand = new RelayCommand(async p => await InstallAsync(p as SkillRowViewModel, false).ConfigureAwait(true), CanMutate);
        PullCommand = new RelayCommand(async p => await InstallAsync(p as SkillRowViewModel, true).ConfigureAwait(true), p => CanMutate(p) && p is SkillRowViewModel pull && pull.CanPull);
        PushCommand = new RelayCommand(async p => await InstallAsync(p as SkillRowViewModel, true).ConfigureAwait(true), p => CanMutate(p) && p is SkillRowViewModel push && push.CanPush);
        UninstallCommand = new RelayCommand(async p => await UninstallAsync(p as SkillRowViewModel).ConfigureAwait(true), CanMutate);
        OpenInstallFolderCommand = new RelayCommand(p => OpenPath((p as SkillRowViewModel)?.InstallFolder), p =>
            p is SkillRowViewModel row && !string.IsNullOrWhiteSpace(row.InstallFolder));
        CloseConflictCommand = new RelayCommand(_ => IsConflictOpen = false);
        OpenConflictFolderCommand = new RelayCommand(_ => OpenPath(ConflictFolder), _ =>
            !string.IsNullOrWhiteSpace(ConflictFolder));
        SelectThemeCommand = new RelayCommand(p => SelectTheme(p as ThemeOptionViewModel));
        SelectSettingsTabCommand = new RelayCommand(p => SelectSettingsTab(p as SettingsTabViewModel));
        _showRepoLinks = _settings.ShowRepoLinks;
        _autoCheckUpdates = _settings.AutoCheckUpdates;
        _nasUser = string.IsNullOrWhiteSpace(_settings.NasUser) ? Environment.UserName : _settings.NasUser;
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
        CopyDiagnosticsCommand = new RelayCommand(_ => Clipboard.SetText(BuildDiagnostics()));
        _scan = new SkillCatalogScan(
            AllSkills, _gh, _git, _skillGit,
            () => NasUser, () => WorkspacePath, () => HasOpenWorkspace, Log,
            ApplyAccount, ApplyNoLanCatalog, ApplyLanProbe, ApplyNasAccess,
            () => { PullCommand.RaiseCanExecuteChanged(); PushCommand.RaiseCanExecuteChanged(); });
        ReloadWorkspaceItems();
        if (!string.IsNullOrWhiteSpace(WorkspacePath) && Directory.Exists(WorkspacePath))
        {
            ApplyWorkspace(WorkspacePath, persist: false, autoSelectDetected: false);
        }
        else
        {
            ResetRootDefaults(Array.Empty<SkillRootInfo>(), CurrentSavedRoots(), autoSelectDetected: true);
            RefreshInstallStatuses();
        }
    }

    public ObservableCollection<RootOptionViewModel> Roots { get; } = [];
    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = [];
    public ObservableCollection<SettingsTabViewModel> SettingsTabs { get; } = [];
    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];
    public ObservableCollection<SkillRowViewModel> AllSkills { get; } = [];
    public ObservableCollection<SkillRowViewModel> VisibleSkills { get; } = [];
    public RelayCommand BrowseCommand { get; }
    public RelayCommand SelectWorkspaceCommand { get; }
    public RelayCommand RemoveWorkspaceCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand InstallCommand { get; }
    public RelayCommand PullCommand { get; }
    public RelayCommand PushCommand { get; }
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
            }
        }
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
    public string WindowTitle => IsDev
        ? "Moon Game Dev Tool Manager — DEV"
        : "Moon Game Dev Tool Manager";

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

    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                ApplyFilter();
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
                PullCommand.RaiseCanExecuteChanged(); PushCommand.RaiseCanExecuteChanged();
                UninstallCommand.RaiseCanExecuteChanged();
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

    public void ApplyWorkspace(string path, bool persist, bool autoSelectDetected)
    {
        var entry = WorkspaceBook.AddOrGet(_settings, path);
        WorkspacePath = entry.Path;
        if (!Directory.Exists(entry.Path))
        {
            ResetRootDefaults(Array.Empty<SkillRootInfo>(), entry.SelectedRoots, autoSelectDetected: false);
            RefreshInstallStatuses();
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
        RefreshInstallStatuses();
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
            await _scan.RefreshAccessAsync().ConfigureAwait(true);
            RefreshInstallStatuses();
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

    private async Task InstallAsync(SkillRowViewModel? row, bool update)
    {
        if (row is null || !TryBeginMutation(row, requireAccess: true, out var roots))
        {
            return;
        }

        Busy = true;
        try
        {
            if (row.IsCompanion && !update)
            {
                Log($"{row.Name} 是随附 Skill，请安装所属 Plugin，不能单独当 Skill 安装。");
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

            if (update && !CanAutoUpdate(row))
            {
                return;
            }

            var branchNote = string.IsNullOrWhiteSpace(row.SelectedBranch) ? "" : $"（{row.SelectedBranch}）";
            Log($"{(update ? "更新" : "安装")} {row.Name}{branchNote}");
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

        if (!row.Definition.IsProjectCopy && roots.Count == 0)
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
        return !Busy && HasOpenWorkspace && parameter is SkillRowViewModel;
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

    private void OnSelectedBranchChanged(object? sender, EventArgs e)
    {
        if (sender is SkillRowViewModel row && HasOpenWorkspace)
        {
            _ = _scan.InspectOneAsync(row);
        }
    }

    private void NotifyWorkspaceState()
    {
        Raise(nameof(HasOpenWorkspace));
        Raise(nameof(IsBrowseOnly));
        Raise(nameof(WorkspaceDisplay));
        Raise(nameof(BrowseOnlyHint));
        InstallCommand?.RaiseCanExecuteChanged();
        PullCommand?.RaiseCanExecuteChanged(); PushCommand?.RaiseCanExecuteChanged();
        UninstallCommand?.RaiseCanExecuteChanged();
        OpenWorkspaceFolderCommand?.RaiseCanExecuteChanged();
        OpenInstallFolderCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshInstallStatuses()
    {
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
        _ = _scan.InspectAllAsync();
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

    private void ApplyFilter()
    {
        VisibleSkills.Clear();
        foreach (var row in AllSkills)
        {
            if (row.IsCompanion && !IsParentPluginInstalled(row))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(Filter)
                || row.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                || row.KindLabel.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                || row.EngineLabel.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                || row.Definition.ParentPluginName.Contains(Filter, StringComparison.OrdinalIgnoreCase))
            {
                VisibleSkills.Add(row);
            }
        }
    }

    private bool IsParentPluginInstalled(SkillRowViewModel row)
    {
        var parent = AllSkills.FirstOrDefault(item =>
            item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));
        return parent?.Installs.Any(item => item.Installed) == true;
    }

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
        text.AppendLine($"App update: {UpdateStatus}");
        text.AppendLine($"Config: {_paths.ConfigDirectory}");
        text.AppendLine($"Cache: {_paths.CacheDirectory}");
        return text.ToString().TrimEnd();
    }

    private void OpenPath(string? path, bool create = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (create && !Directory.Exists(path) && !File.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Log($"找不到 {path}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log($"无法打开 {path}: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrWhiteSpace(LogText) ? line : LogText + Environment.NewLine + line;
    }
}
