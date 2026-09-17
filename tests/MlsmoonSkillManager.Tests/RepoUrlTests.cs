using MlsmoonSkillManager.Core.Services;
using Xunit;

namespace MlsmoonSkillManager.Tests;

public class RepoUrlTests
{
    [Theory]
    [InlineData("https://github.com/MlsMoon/open-psd-kit", "MlsMoon", "open-psd-kit")]
    [InlineData("https://github.com/MlsMoon/open-psd-kit.git", "MlsMoon", "open-psd-kit")]
    [InlineData("git@github.com:MlsMoon/SpineGpuSkinning.git", "MlsMoon", "SpineGpuSkinning")]
    [InlineData("MlsMoon/3d-model-data-reader", "MlsMoon", "3d-model-data-reader")]
    public void Parse_AcceptedForms(string input, string owner, string name)
    {
        Assert.True(RepoUrl.TryParse(input, out var repo));
        Assert.Equal(owner, repo.Owner);
        Assert.Equal(name, repo.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a repo")]
    [InlineData("https://gitlab.com/MlsMoon/open-psd-kit")]
    public void Parse_RejectsInvalid(string input)
    {
        Assert.False(RepoUrl.TryParse(input, out _));
    }
}
