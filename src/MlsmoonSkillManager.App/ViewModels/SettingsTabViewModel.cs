namespace MlsmoonSkillManager.App.ViewModels;

public sealed class SettingsTabViewModel : ObservableObject
{
    private bool _isSelected;

    public SettingsTabViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
