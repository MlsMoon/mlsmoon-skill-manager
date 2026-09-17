using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class ThemeOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public ThemeOptionViewModel(string id)
    {
        Id = ThemeResolver.Normalize(id);
        Label = ThemeResolver.Label(Id);
    }

    public string Id { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                Raise(nameof(ButtonStyleKey));
            }
        }
    }

    public string ButtonStyleKey => IsSelected ? "ThemeButtonSelected" : "ThemeButton";
}
