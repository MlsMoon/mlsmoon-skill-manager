using System.Collections.ObjectModel;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class FilterChip : ObservableObject
{
    private bool _isSelected;

    public FilterChip(string group, string id, string label, bool selected)
    {
        Group = group;
        Id = id;
        Label = label;
        _isSelected = selected;
    }

    public string Group { get; }
    public string Id { get; }
    public string Label { get; }
    public string AutomationId => $"Filter-{Group}-{Id}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class CatalogFilter : ObservableObject
{
    private string _query = "";
    private string _summary = "";

    public CatalogFilter()
    {
        Installs = Create("install", ("all", "全部"), ("installed", "已安装"), ("missing", "未安装"));
        Kinds = Create("kind", ("all", "全部"), ("skill", "Skill"), ("plugin", "Plugin"), ("package", "Package"));
        Engines = Create("engine", ("all", "全部"), ("unity", "Unity"), ("godot", "Godot"), ("universal", "全引擎"));
        SelectChipCommand = new RelayCommand(p => Select(p as FilterChip));
    }

    public event Action? Changed;

    public ObservableCollection<FilterChip> Installs { get; }
    public ObservableCollection<FilterChip> Kinds { get; }
    public ObservableCollection<FilterChip> Engines { get; }
    public RelayCommand SelectChipCommand { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                Changed?.Invoke();
            }
        }
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public void Fill(IList<SkillRowViewModel> all, IList<SkillRowViewModel> visible)
    {
        visible.Clear();
        var listed = 0;
        foreach (var row in CatalogOrder.Sort(all, item => item.Definition))
        {
            if (row.IsCompanion && !ParentInstalled(row, all))
            {
                continue;
            }

            listed++;
            if (Matches(row, all))
            {
                visible.Add(row);
            }
        }

        Summary = $"显示 {visible.Count} / {listed}";
    }

    private bool Matches(SkillRowViewModel row, IList<SkillRowViewModel> all)
    {
        var install = Selected(Installs);
        if (install == "installed" && !row.IsInstalled)
        {
            return false;
        }

        if (install == "missing" && row.IsInstalled)
        {
            return false;
        }

        if (!MatchesKind(row, all, Selected(Kinds)))
        {
            return false;
        }

        var engine = Selected(Engines);
        if (engine == "universal" && !row.Definition.IsUniversal)
        {
            return false;
        }

        if (engine is "unity" or "godot"
            && !row.Definition.ResolvedEngines.Contains(engine, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesQuery(row);
    }

    private bool MatchesQuery(SkillRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return true;
        }

        return row.Name.Contains(Query, StringComparison.OrdinalIgnoreCase)
               || row.Description.Contains(Query, StringComparison.OrdinalIgnoreCase)
               || row.KindLabel.Contains(Query, StringComparison.OrdinalIgnoreCase)
               || row.EngineLabel.Contains(Query, StringComparison.OrdinalIgnoreCase)
               || row.Definition.ParentPluginName.Contains(Query, StringComparison.OrdinalIgnoreCase);
    }

    private void Select(FilterChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        var group = Installs;
        if (chip.Group == "kind")
        {
            group = Kinds;
        }
        else if (chip.Group == "engine")
        {
            group = Engines;
        }

        foreach (var item in group)
        {
            item.IsSelected = item == chip;
        }

        Changed?.Invoke();
    }

    private static string Selected(IEnumerable<FilterChip> group) =>
        group.FirstOrDefault(item => item.IsSelected)?.Id ?? "all";

    private static bool MatchesKind(SkillRowViewModel row, IEnumerable<SkillRowViewModel> all, string kind)
    {
        if (kind is "all" or "")
        {
            return true;
        }

        var parent = Parent(row, all);
        return kind switch
        {
            "skill" => row.Definition.Kind == ToolKind.Skill,
            "plugin" => row.IsPlugin || (row.IsCompanion && parent?.IsPlugin == true),
            "package" => row.Definition.IsPackage || (row.IsCompanion && parent?.Definition.IsPackage == true),
            _ => true
        };
    }

    private static bool ParentInstalled(SkillRowViewModel row, IEnumerable<SkillRowViewModel> all) =>
        Parent(row, all)?.IsInstalled == true;

    private static SkillRowViewModel? Parent(SkillRowViewModel row, IEnumerable<SkillRowViewModel> all) =>
        all.FirstOrDefault(item =>
            item.Definition.Id.Equals(row.Definition.ParentPluginId, StringComparison.OrdinalIgnoreCase));

    private static ObservableCollection<FilterChip> Create(string group, params (string Id, string Label)[] items)
    {
        return [.. items.Select((item, index) => new FilterChip(group, item.Id, item.Label, index == 0))];
    }
}
