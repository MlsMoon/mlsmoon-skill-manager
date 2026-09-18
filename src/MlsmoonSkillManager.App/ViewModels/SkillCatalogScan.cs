using System.Windows;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class SkillCatalogScan
{
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
    private ScanMeter? _meter;

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

    public async Task RefreshAccessAsync()
    {
        if (SkipLiveScan())
        {
            return;
        }

        var rows = _rows.ToList();
        var tops = rows.Where(item => !item.IsCompanion).ToList();
        var inspect = _hasWorkspace() ? rows.Count : 0;
        _meter = new ScanMeter(rows, 2 + tops.Count + inspect);
        foreach (var row in rows)
        {
            row.Access = new RepoAccess { State = AccessState.Checking, Message = "正在检查权限…" };
            row.MarkGitChecking();
        }

        _meter.Show(null, "正在检查 GitHub 账号…");
        var account = await _gh.GetAccountStatusAsync().ConfigureAwait(true);
        _applyAccount(account);
        _log(account.Detail);
        _meter.Advance(null, "正在检查 GitHub 账号…");

        var lanHost = rows.Select(item => item.Definition)
            .FirstOrDefault(item => item.IsLan && !string.IsNullOrWhiteSpace(item.Host))
            ?.Host;
        if (string.IsNullOrWhiteSpace(lanHost))
        {
            _applyNoLan();
            _log("未配置局域网条目。");
            _meter.Advance(null, "未配置局域网");
        }
        else
        {
            _meter.Show(null, "正在探测局域网…");
            var probe = await LanNetwork.ProbeAsync(lanHost).ConfigureAwait(true);
            _applyLan(probe);
            _log(probe.Detail);
            _meter.Advance(null, "正在探测局域网…");
        }

        foreach (var row in tops)
        {
            _meter.Show(row, "正在检查权限…");
            await CheckAccessAsync(row, account).ConfigureAwait(true);
            _meter.Advance(row, "正在检查权限…");
        }

        foreach (var row in rows.Where(item => item.IsCompanion))
        {
            var parent = rows.FirstOrDefault(item =>
                item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));
            row.Access = parent?.Access ?? new RepoAccess
            {
                State = AccessState.NoPermission,
                Message = "所属 Plugin 当前无权限访问"
            };
        }

        foreach (var row in rows)
        {
            await _meta.RefreshAsync(row, null, row.Access.CanInstall).ConfigureAwait(true);
        }
    }

    public async Task InspectAllAsync()
    {
        if (SkipLiveScan())
        {
            return;
        }

        _gitCts?.Cancel();
        _gitCts = new CancellationTokenSource();
        var ct = _gitCts.Token;
        if (!_hasWorkspace())
        {
            foreach (var row in _rows)
            {
                row.ApplyGit(SkillGitStatus.Empty);
            }

            _raiseUpdate();
            return;
        }

        foreach (var row in _rows)
        {
            row.MarkGitChecking();
        }

        if (_meter is null || _meter.Done >= _meter.Total)
        {
            _meter = new ScanMeter(_rows, _rows.Count);
        }

        _meter.Show(null, "正在对照 Git…");
        try
        {
            var editor = UnityWorkspace.ReadEditorVersion(_workspace());
            foreach (var row in _rows.ToList())
            {
                ct.ThrowIfCancellationRequested();
                _meter.Show(row, "正在对照 Git…");
                await InspectCoreAsync(row, editor, ct).ConfigureAwait(true);
                _meter.Advance(row, "正在对照 Git…");
            }
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
        row.SetScan("正在对照 Git…", 20);
        await InspectCoreAsync(row, null, CancellationToken.None).ConfigureAwait(true);
        if (row.IsLoading)
        {
            row.SetScan("正在对照 Git…", 100);
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

    private async Task CheckAccessAsync(SkillRowViewModel row, GhAccountStatus account)
    {
        if (row.Definition.IsLan)
        {
            row.Access = await _git.CheckLanAccessAsync(row.Definition, _nasUser()).ConfigureAwait(true);
            _applyNas(row.Access);
            if (!row.Access.CanInstall)
            {
                _log($"{row.Name}: {row.Access.Message}");
            }

            return;
        }

        if (!RepoUrl.TryParse(row.Definition.Repo, out var repo))
        {
            row.Access = new RepoAccess
            {
                State = AccessState.NoPermission,
                Message = "当前无权限访问"
            };
            return;
        }

        row.Access = await _gh.CheckRepoAccessAsync(repo, account).ConfigureAwait(true);
        if (row.Access.State == AccessState.NoPermission)
        {
            _log($"{row.Name}: 当前无权限访问");
        }
    }

    private async Task InspectCoreAsync(SkillRowViewModel row, string? editor, CancellationToken cancellationToken)
    {
        editor ??= UnityWorkspace.ReadEditorVersion(_workspace());
        var status = await _skillGit.InspectAsync(
                row.Definition,
                _workspace(),
                row.Installs,
                row.SelectedBranch,
                _nasUser(),
                editor,
                row.Access.CanInstall,
                cancellationToken)
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
}

internal sealed class ScanMeter
{
    private readonly IReadOnlyList<SkillRowViewModel> _rows;

    public ScanMeter(IEnumerable<SkillRowViewModel> rows, int total)
    {
        _rows = rows as IReadOnlyList<SkillRowViewModel> ?? rows.ToList();
        Total = Math.Max(1, total);
    }

    public int Total { get; }
    public int Done { get; private set; }
    public int Percent => (int)Math.Clamp(Math.Round(100.0 * Done / Total), 0, 100);

    public void Show(SkillRowViewModel? current, string caption) => Paint(current, caption);

    public void Advance(SkillRowViewModel? current, string caption)
    {
        Done = Math.Min(Total, Done + 1);
        Paint(current, caption);
    }

    private void Paint(SkillRowViewModel? current, string caption)
    {
        foreach (var row in _rows)
        {
            if (!row.IsLoading)
            {
                continue;
            }

            var text = current is null || ReferenceEquals(row, current) ? caption : "排队扫描…";
            row.SetScan(text, Percent);
        }
    }
}
