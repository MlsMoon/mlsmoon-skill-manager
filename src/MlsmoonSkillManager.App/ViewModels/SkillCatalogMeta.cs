using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

internal sealed class SkillCatalogMeta
{
    private readonly CatalogMetaSync _sync;
    private readonly IList<SkillRowViewModel> _rows;

    public SkillCatalogMeta(
        IList<SkillRowViewModel> rows,
        GhCli gh,
        GitRemote git,
        SkillGit skillGit,
        Func<string> nasUser,
        Func<string> workspace)
    {
        _rows = rows;
        _sync = new CatalogMetaSync(new AppPaths(), gh, git, skillGit, nasUser, workspace);
        ApplyCached();
    }

    public void ApplyCached()
    {
        _sync.ApplyCached(_rows.Select(item => item.Definition), CatalogMetaSync.Apply);
        foreach (var row in _rows)
        {
            if (row.IsCompanion)
            {
                var parent = ParentOf(row);
                if (parent is not null)
                {
                    row.Definition.ParentPluginName = parent.Definition.DisplayName;
                }
            }

            row.ApplyDisplay();
        }
    }

    public async Task RefreshAsync(
        SkillRowViewModel row,
        string? commit,
        bool canReach,
        CancellationToken cancellationToken = default)
    {
        if (!canReach)
        {
            return;
        }

        var entry = await _sync.EnsureAsync(
                row.Definition,
                commit,
                row.SelectedBranch,
                canReach,
                cancellationToken)
            .ConfigureAwait(true);
        if (entry is null)
        {
            return;
        }

        CatalogMetaSync.Apply(row.Definition, entry);
        row.ReadmeExcerpt = entry.Description;
        row.ApplyDisplay();
        if (!row.Definition.IsProjectCopy)
        {
            return;
        }

        foreach (var companion in _rows.Where(item =>
                     item.Definition.ParentPluginId.Equals(row.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            companion.Definition.ParentPluginName = row.Definition.DisplayName;
            companion.ApplyDisplay();
        }
    }

    private SkillRowViewModel? ParentOf(SkillRowViewModel row) =>
        _rows.FirstOrDefault(item =>
            item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));
}
