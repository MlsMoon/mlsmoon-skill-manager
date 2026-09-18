using System.Windows;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class SkillCatalogScan
{
    private const int QueuedAfterAccess = 36;

    private readonly IList<SkillRowViewModel> _rows;
    private readonly GhCli _gh;
    private readonly GitRemote _git;
    private readonly SkillGit _skillGit;
    private readonly Func<string> _nasUser;
    private readonly Func<string> _workspace;
    private readonly Func<bool> _hasWorkspace;
    private readonly Action<string> _log;
    private readonly Action<GhAccountStatus> _applyAccount;
    private readonly Action _applyNoLan;
    private readonly Action<LanProbe> _applyLan;
    private readonly Action<RepoAccess> _applyNas;
    private readonly Action _raiseUpdate;
    private readonly SkillCatalogMeta _meta;
    private CancellationTokenSource? _gitCts;

    public SkillCatalogScan(
        IList<SkillRowViewModel> rows,
        GhCli gh,
        GitRemote git,
        SkillGit skillGit,
        Func<string> nasUser,
        Func<string> workspace,
        Func<bool> hasWorkspace,
        Action<string> log,
        Action<GhAccountStatus> applyAccount,
        Action applyNoLan,
        Action<LanProbe> applyLan,
        Action<RepoAccess> applyNas,
        Action raiseUpdate)
    {
        _rows = rows;
        _gh = gh;
        _git = git;
        _skillGit = skillGit;
        _nasUser = nasUser;
        _workspace = workspace;
        _hasWorkspace = hasWorkspace;
        _log = log;
        _applyAccount = applyAccount;
        _applyNoLan = applyNoLan;
        _applyLan = applyLan;
        _applyNas = applyNas;
        _raiseUpdate = raiseUpdate;
        _meta = new SkillCatalogMeta(rows, gh, git, skillGit, nasUser, workspace);
    }

    public async Task ScanAllAsync()
    {
        if (SkipLiveScan())
        {
            return;
        }

        var ct = BeginGitCts();
        try
        {
            await AccessAllAsync(ct).ConfigureAwait(true);
            await InspectQueuedAsync(ct, continueProgress: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task InspectAllAsync()
    {
        if (SkipLiveScan())
        {
            return;
        }

        var ct = BeginGitCts();
        try
        {
            await InspectQueuedAsync(ct, continueProgress: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task InspectOneAsync(SkillRowViewModel row)
    {
        if (!_hasWorkspace())
        {
            return;
        }

        row.MarkGitChecking();
        if (row.Definition.IsRouting)
        {
            await InspectRoutingAsync(row, CancellationToken.None, continueProgress: false)
                .ConfigureAwait(true);
            return;
        }

        await InspectCoreAsync(row, null, CancellationToken.None, continueProgress: false)
            .ConfigureAwait(true);
    }

    private CancellationToken BeginGitCts()
    {
        _gitCts?.Cancel();
        _gitCts = new CancellationTokenSource();
        return _gitCts.Token;
    }

    private async Task AccessAllAsync(CancellationToken cancellationToken)
    {
        var rows = _rows.ToList();
        var tops = rows.Where(item => !item.IsCompanion).ToList();
        foreach (var row in rows)
        {
            if (!row.IsCompanion)
            {
                row.Access = new RepoAccess { State = AccessState.Checking, Message = "正在检查权限…" };
            }

            row.MarkGitChecking();
            row.SetScan("排队扫描…", 0);
        }

        Caption(rows, "正在检查 GitHub 账号…", 8);
        var account = await _gh.GetAccountStatusAsync().ConfigureAwait(true);
        _applyAccount(account);
        _log(account.Detail);

        var lanHost = rows.Select(item => item.Definition)
            .FirstOrDefault(item => item.IsLan && !string.IsNullOrWhiteSpace(item.Host))
            ?.Host;
        if (string.IsNullOrWhiteSpace(lanHost))
        {
            _applyNoLan();
            _log("未配置局域网条目。");
        }
        else
        {
            Caption(rows, "正在探测局域网…", 16);
            var probe = await LanNetwork.ProbeAsync(lanHost).ConfigureAwait(true);
            _applyLan(probe);
            _log(probe.Detail);
        }

        foreach (var row in tops)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Caption(rows, "排队扫描…", 0, except: row);
            row.SetScan("正在检查权限…", 22);
            var access = await CheckAccessAsync(row, account).ConfigureAwait(true);
            row.SetScan("正在读取名称…", 30);
            await _meta.RefreshAsync(row, null, access.CanInstall, cancellationToken).ConfigureAwait(true);
            row.Access = access;
            row.SetScan("排队扫描…", QueuedAfterAccess);
        }

        foreach (var row in rows.Where(item => item.IsCompanion))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parent = ParentOf(row);
            row.Access = parent?.Access ?? new RepoAccess
            {
                State = AccessState.NoPermission,
                Message = "所属 Plugin 当前无权限访问"
            };
            await _meta.RefreshAsync(row, null, row.Access.CanInstall, cancellationToken).ConfigureAwait(true);
            row.SetScan("排队扫描…", QueuedAfterAccess);
        }
    }

    private async Task InspectQueuedAsync(CancellationToken cancellationToken, bool continueProgress)
    {
        if (!_hasWorkspace())
        {
            foreach (var row in _rows)
            {
                row.ApplyGit(SkillGitStatus.Empty);
            }

            _raiseUpdate();
            return;
        }

        var pending = _rows.ToList();
        var queued = continueProgress ? QueuedAfterAccess : 0;
        if (!continueProgress)
        {
            foreach (var row in pending)
            {
                row.MarkGitChecking();
                row.SetScan("排队扫描…", 0);
            }
        }

        var editor = UnityWorkspace.ReadEditorVersion(_workspace());
        var routing = pending.Where(item => item.Definition.IsRouting).ToList();
        var gitRows = pending.Where(item => !item.Definition.IsRouting).ToList();
        foreach (var row in routing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Caption(pending, "排队扫描…", queued, except: row);
            await InspectRoutingAsync(row, cancellationToken, continueProgress).ConfigureAwait(true);
        }

        foreach (var row in gitRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Caption(pending, "排队扫描…", queued, except: row);
            await InspectCoreAsync(row, editor, cancellationToken, continueProgress).ConfigureAwait(true);
        }
    }

    private bool SkipLiveScan()
    {
        if (Application.Current is not App { IsUiTest: true })
        {
            return false;
        }

        foreach (var row in _rows)
        {
            row.Access = new RepoAccess { State = AccessState.Accessible, Message = "ui-test" };
            row.ApplyGit(SkillGitStatus.Empty);
        }

        return true;
    }

    private async Task<RepoAccess> CheckAccessAsync(SkillRowViewModel row, GhAccountStatus account)
    {
        if (row.Definition.IsLan)
        {
            var lan = await _git.CheckLanAccessAsync(row.Definition, _nasUser()).ConfigureAwait(true);
            _applyNas(lan);
            if (!lan.CanInstall)
            {
                _log($"{row.Name}: {lan.Message}");
            }

            return lan;
        }

        if (!RepoUrl.TryParse(row.Definition.Repo, out var repo))
        {
            return new RepoAccess
            {
                State = AccessState.NoPermission,
                Message = "当前无权限访问"
            };
        }

        var access = await _gh.CheckRepoAccessAsync(repo, account).ConfigureAwait(true);
        if (access.State == AccessState.NoPermission)
        {
            _log($"{row.Name}: 当前无权限访问");
        }

        return access;
    }

    private async Task InspectRoutingAsync(
        SkillRowViewModel row,
        CancellationToken cancellationToken,
        bool continueProgress)
    {
        row.SetScan("对照路由源…", continueProgress ? 55 : 40);
        var parent = ParentOf(row);
        var align = parent is null
            ? new RoutingSkillAlign()
            : await Task.Run(
                    () => RoutingSkillSource.Inspect(
                        parent.Definition,
                        row.Definition,
                        _workspace(),
                        SkillRoots.DetectableRoots),
                    cancellationToken)
                .ConfigureAwait(true);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        row.ApplyRouting(align);
        await _meta.RefreshAsync(row, null, row.Access.CanInstall, cancellationToken).ConfigureAwait(true);
        _raiseUpdate();
    }

    private async Task InspectCoreAsync(
        SkillRowViewModel row,
        string? editor,
        CancellationToken cancellationToken,
        bool continueProgress)
    {
        editor ??= UnityWorkspace.ReadEditorVersion(_workspace());
        var progress = new Progress<ScanProgress>(step =>
            row.SetScan(step.Text, continueProgress ? 40 + step.Percent * 60 / 100 : step.Percent));
        var status = await _skillGit.InspectAsync(
                row.Definition,
                _workspace(),
                row.Installs,
                row.HasUserPickedBranch ? row.SelectedBranch : null,
                _nasUser(),
                editor,
                row.Access.CanInstall,
                cancellationToken,
                progress)
            .ConfigureAwait(true);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        row.ApplyGit(status);
        await _meta.RefreshAsync(row, status.RemoteCommit, row.Access.CanInstall, cancellationToken)
            .ConfigureAwait(true);
        _raiseUpdate();
    }

    private SkillRowViewModel? ParentOf(SkillRowViewModel row) =>
        _rows.FirstOrDefault(item =>
            item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));

    private static void Caption(
        IEnumerable<SkillRowViewModel> rows,
        string text,
        int percent,
        SkillRowViewModel? except = null)
    {
        foreach (var row in rows)
        {
            if (!row.IsLoading || ReferenceEquals(row, except))
            {
                continue;
            }

            row.SetScan(text, percent);
        }
    }
}
