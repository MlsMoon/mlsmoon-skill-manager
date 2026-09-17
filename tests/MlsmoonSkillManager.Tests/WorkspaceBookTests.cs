using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;
using Xunit;

namespace MlsmoonSkillManager.Tests;

public class WorkspaceBookTests
{
    [Fact]
    public void Migrate_ImportsLastWorkspace()
    {
        var settings = new UserSettings
        {
            LastWorkspace = @"D:\UnityProjects\LyingBottleAnother",
            SelectedRoots = [".agent", ".claude"]
        };

        WorkspaceBook.Migrate(settings);

        var entry = Assert.Single(settings.Workspaces);
        Assert.True(WorkspaceBook.PathsEqual(entry.Path, settings.LastWorkspace));
        Assert.Equal(new[] { ".agent", ".claude" }, entry.SelectedRoots);
    }

    [Fact]
    public void AddOrGet_DedupsAndMovesToFront()
    {
        var settings = new UserSettings();
        var first = WorkspaceBook.AddOrGet(settings, @"D:\OtherProjects\a");
        WorkspaceBook.AddOrGet(settings, @"D:\OtherProjects\b");
        var again = WorkspaceBook.AddOrGet(settings, @"D:\OtherProjects\a\");

        Assert.Same(first, again);
        Assert.Equal(2, settings.Workspaces.Count);
        Assert.True(WorkspaceBook.PathsEqual(settings.Workspaces[0].Path, first.Path));
        Assert.True(WorkspaceBook.PathsEqual(settings.LastWorkspace, first.Path));
    }

    [Fact]
    public void Remove_UpdatesLastWorkspace()
    {
        var settings = new UserSettings();
        WorkspaceBook.AddOrGet(settings, @"D:\ws\one");
        WorkspaceBook.AddOrGet(settings, @"D:\ws\two");
        Assert.True(WorkspaceBook.Remove(settings, @"D:\ws\two"));
        Assert.True(WorkspaceBook.PathsEqual(settings.LastWorkspace, @"D:\ws\one"));
        Assert.Single(settings.Workspaces);
    }

    [Fact]
    public void DisplayName_UsesFolderName()
    {
        Assert.Equal("LyingBottleAnother", WorkspaceBook.DisplayName(@"D:\UnityProjects\LyingBottleAnother\"));
    }
}
