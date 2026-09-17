using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class RootOptionViewModel : ObservableObject
{
    private bool _isSelected;
    private string _badge = "";

    public RootOptionViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool IsDefault => Name.Equals(SkillRoots.DefaultRoot, StringComparison.OrdinalIgnoreCase);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Badge
    {
        get => _badge;
        set => SetProperty(ref _badge, value);
    }

    public string Label => IsDefault ? $"{Name}（默认）" : Name;
}
