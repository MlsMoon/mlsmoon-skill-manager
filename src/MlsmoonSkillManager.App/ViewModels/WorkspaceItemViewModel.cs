using System.IO;
using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.ViewModels;

public sealed class WorkspaceItemViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _exists;

    public WorkspaceItemViewModel(WorkspaceEntry entry)
    {
        Path = WorkspaceBook.Normalize(entry.Path);
        Name = WorkspaceBook.DisplayName(Path);
        RefreshExists();
    }

    public string Path { get; }
    public string Name { get; }

    public bool Exists
    {
        get => _exists;
        private set
        {
            if (SetProperty(ref _exists, value))
            {
                Raise(nameof(Status));
                Raise(nameof(IsMissing));
            }
        }
    }

    public string Status => Exists ? Path : "路径不存在";
    public bool IsMissing => !Exists;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshExists()
    {
        Exists = Directory.Exists(Path);
    }
}
