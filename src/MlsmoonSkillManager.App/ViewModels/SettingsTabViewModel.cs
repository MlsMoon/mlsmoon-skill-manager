namespace MlsmoonSkillManager.App.ViewModels;

public sealed class SettingsTabViewModel : ObservableObject
{
    private bool _isSelected;

    public SettingsTabViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public static IEnumerable<SettingsTabViewModel> CreateAll(bool isDev, string selectedId = "appearance")
    {
        foreach (var (id, label) in Enumerate(isDev))
        {
            yield return new SettingsTabViewModel(id, label)
            {
                IsSelected = id == selectedId
            };
        }
    }

    private static IEnumerable<(string Id, string Label)> Enumerate(bool isDev)
    {
        yield return ("appearance", "外观");
        yield return ("connection", "连接");
        if (!isDev)
        {
            yield return ("updates", "更新");
        }

        yield return ("folders", "位置");
        yield return ("about", "关于");
        yield return ("log", "日志");
    }

    public string Id { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
