using MlsmoonSkillManager.Core.Models;
using MlsmoonSkillManager.Core.Services;
using Xunit;

namespace MlsmoonSkillManager.Tests;

public class CatalogAndInstallTests
{
    [Fact]
    public void Catalog_MergesOverrideById()
    {
        var root = CreateTempDir();
        try
        {
            var appDir = Path.Combine(root, "app");
            Directory.CreateDirectory(Path.Combine(appDir, "catalog"));
            File.WriteAllText(
                Path.Combine(appDir, "catalog", "skills.json"),
                """
                {"version":1,"skills":[{"id":"alpha","name":"alpha","repo":"https://github.com/MlsMoon/alpha"}]}
                """);
            var paths = new AppPaths(appDir, Path.Combine(root, "config"), Path.Combine(root, "cache"));
            Directory.CreateDirectory(paths.ConfigDirectory);
            File.WriteAllText(
                paths.UserOverridePath,
                """
                {"version":1,"skills":[{"id":"alpha","name":"alpha-renamed","repo":"https://github.com/MlsMoon/alpha-private"}]}
                """);

            var catalog = new CatalogStore(paths).Load();
            var skill = Assert.Single(catalog.Skills);
            Assert.Equal("alpha-renamed", skill.Name);
            Assert.Equal("https://github.com/MlsMoon/alpha-private", skill.Repo);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void WorkspaceScanner_DetectsKnownRoots()
    {
        var workspace = CreateTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(workspace, ".agent", "skills"));
            Directory.CreateDirectory(Path.Combine(workspace, ".claude"));
            var info = new WorkspaceScanner().Scan(workspace);
            Assert.Contains(info.Roots, r => r.Name == ".agents" && r.Exists && r.HasSkillsFolder && r.HasLegacyOnly);
            Assert.Contains(info.Roots, r => r.Name == ".claude" && r.Exists && !r.HasSkillsFolder);
            Assert.All(info.Roots.Where(r => r.Name is ".codex" or ".grok"), r => Assert.False(r.Exists));
        }
        finally
        {
            TryDelete(workspace);
        }
    }

    [Fact]
    public void SkillInstaller_CopiesSourceAndWritesMarker()
    {
        var root = CreateTempDir();
        try
        {
            var source = Path.Combine(root, "src");
            var dest = Path.Combine(root, ".agent", "skills", "demo");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "SKILL.md"), "---\nname: demo\n---\n");
            Directory.CreateDirectory(Path.Combine(source, ".git"));
            File.WriteAllText(Path.Combine(source, ".git", "HEAD"), "ref");
            SkillInstaller.CopySkill(source, dest);
            SkillInstaller.WriteMarker(dest, new InstallMarker { Id = "demo", Repo = "https://github.com/MlsMoon/demo", Commit = "abc" });
            Assert.True(File.Exists(Path.Combine(dest, "SKILL.md")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".git")));
            Assert.Equal("demo", SkillInstaller.ReadMarker(Path.Combine(dest, SkillInstaller.MarkerFileName))?.Id);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Catalog_LoadsPluginsWithSeparateKind()
    {
        var root = CreateTempDir();
        try
        {
            var appDir = Path.Combine(root, "app");
            Directory.CreateDirectory(Path.Combine(appDir, "catalog"));
            File.WriteAllText(
                Path.Combine(appDir, "catalog", "skills.json"),
                """
                {
                  "version":1,
                  "skills":[{"id":"alpha","name":"alpha","repo":"https://github.com/MlsMoon/alpha"}],
                  "plugins":[{"id":"spine-gpu-skinning","name":"SpineGpuSkinning","repo":"https://github.com/MlsMoon/SpineGpuSkinning","installName":"SpineGpuSkinning","installPath":"Assets/Plugins/SpineGpuSkinning"}],
                  "packages":[{"id":"urp-package-igp","name":"UrpPackageIGP","repo":"https://github.com/MlsMoon/UrpPackageIGP","installName":"UrpPackageIGP"}]
                }
                """);
            var paths = new AppPaths(appDir, Path.Combine(root, "config"), Path.Combine(root, "cache"));
            var catalog = new CatalogStore(paths).Load();
            Assert.Equal(3, catalog.Skills.Count);
            var skill = Assert.Single(catalog.Skills, s => s.Id == "alpha");
            Assert.Equal(ToolKind.Skill, skill.Kind);
            var plugin = Assert.Single(catalog.Skills, s => s.Id == "spine-gpu-skinning");
            Assert.Equal(ToolKind.Plugin, plugin.Kind);
            Assert.Equal(Path.Combine("Assets", "Plugins", "SpineGpuSkinning"), plugin.ResolvedInstallPath);
            var package = Assert.Single(catalog.Skills, s => s.Id == "urp-package-igp");
            Assert.Equal(ToolKind.Package, package.Kind);
            Assert.Equal(Path.Combine("Packages", "UrpPackageIGP"), package.ResolvedInstallPath);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Catalog_AttachesCompanionSkillsToPlugin()
    {
        var root = CreateTempDir();
        try
        {
            var appDir = Path.Combine(root, "app");
            Directory.CreateDirectory(Path.Combine(appDir, "catalog"));
            File.WriteAllText(
                Path.Combine(appDir, "catalog", "skills.json"),
                """
                {
                  "version":1,
                  "skills":[{"id":"alpha","name":"alpha","repo":"https://github.com/MlsMoon/alpha"}],
                  "plugins":[{
                    "id":"spine-gpu-skinning",
                    "name":"SpineGpuSkinning",
                    "repo":"https://github.com/MlsMoon/SpineGpuSkinning",
                    "companionSkills":[
                      {"id":"gpuspine-use-plugin","sourcePath":"Skills~/gpuspine-use-plugin"},
                      {"id":"gpuspine-develop-plugin","sourcePath":"Skills~/gpuspine-develop-plugin"}
                    ]
                  }]
                }
                """);
            var paths = new AppPaths(appDir, Path.Combine(root, "config"), Path.Combine(root, "cache"));
            var catalog = new CatalogStore(paths).Load();
            var standalone = catalog.Skills.Where(s => !s.IsCompanion && !s.IsPlugin).ToList();
            Assert.Equal("alpha", Assert.Single(standalone).Id);
            var plugin = Assert.Single(catalog.Skills, s => s.IsPlugin);
            Assert.Equal(2, plugin.CompanionSkills.Count);
            var use = Assert.Single(catalog.Skills, s => s.Id == "gpuspine-use-plugin");
            Assert.Equal(ToolKind.Companion, use.Kind);
            Assert.Equal("spine-gpu-skinning", use.ParentPluginId);
            Assert.Equal("SpineGpuSkinning", use.ParentPluginName);
            Assert.Equal("https://github.com/MlsMoon/SpineGpuSkinning", use.Repo);
            Assert.Equal("Skills~/gpuspine-use-plugin", use.ResolvedSourcePath);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Catalog_MarksUniversalSkillsAndUnityOnlySpinePlugin()
    {
        var root = CreateTempDir();
        try
        {
            var appDir = Path.Combine(root, "app");
            Directory.CreateDirectory(Path.Combine(appDir, "catalog"));
            File.WriteAllText(
                Path.Combine(appDir, "catalog", "skills.json"),
                """
                {
                  "version":1,
                  "skills":[
                    {"id":"open-psd-kit","name":"open-psd-kit","repo":"https://github.com/MlsMoon/open-psd-kit","engines":["all"]},
                    {"id":"unity-operate-vfx-graph","name":"unity-operate-vfx-graph","repo":"https://github.com/MlsMoon/unity-vfx","engines":["unity"]}
                  ],
                  "plugins":[{
                    "id":"spine-gpu-skinning",
                    "name":"SpineGpuSkinning",
                    "repo":"https://github.com/MlsMoon/SpineGpuSkinning",
                    "engines":["unity"],
                    "companionSkills":[{"id":"gpuspine-use-plugin","sourcePath":"Skills~/gpuspine-use-plugin"}]
                  }]
                }
                """);
            var paths = new AppPaths(appDir, Path.Combine(root, "config"), Path.Combine(root, "cache"));
            var catalog = new CatalogStore(paths).Load();
            var psd = Assert.Single(catalog.Skills, s => s.Id == "open-psd-kit");
            Assert.True(psd.IsUniversal);
            Assert.Equal(GameEngines.Supported, psd.ResolvedEngines);
            var vfx = Assert.Single(catalog.Skills, s => s.Id == "unity-operate-vfx-graph");
            Assert.Equal([GameEngines.Unity], vfx.ResolvedEngines);
            var plugin = Assert.Single(catalog.Skills, s => s.IsPlugin);
            Assert.Equal([GameEngines.Unity], plugin.ResolvedEngines);
            Assert.False(plugin.IsUniversal);
            var companion = Assert.Single(catalog.Skills, s => s.Id == "gpuspine-use-plugin");
            Assert.Equal([GameEngines.Unity], companion.ResolvedEngines);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void GameEngines_MissingOrUnknownBecomesAllSupported()
    {
        Assert.Equal(GameEngines.Supported, GameEngines.Normalize(null));
        Assert.Equal(GameEngines.Supported, GameEngines.Normalize([]));
        Assert.Equal(GameEngines.Supported, GameEngines.Normalize(["unreal"]));
        Assert.Equal([GameEngines.Godot], GameEngines.Normalize(["godot", "unreal"]));
    }

    [Fact]
    public void ResolveSource_RequiresSkillMarkdown()
    {
        var cache = CreateTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(cache, "Skills~", "one"));
            Assert.Throws<InvalidOperationException>(() =>
                SkillInstaller.ResolveSource(cache, "Skills~/one", "one"));
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public void ResolveSource_PluginDoesNotRequireSkillMarkdown()
    {
        var cache = CreateTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(cache, "Runtime"));
            var source = SkillInstaller.ResolveSource(cache, ".", "SpineGpuSkinning", requireSkillMarkdown: false);
            Assert.Equal(Path.GetFullPath(cache), source);
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public void PluginInstaller_CopiesIntoAssetsPluginsAndSkipsSkillsTilde()
    {
        var root = CreateTempDir();
        try
        {
            var source = Path.Combine(root, "src");
            var dest = Path.Combine(root, "Assets", "Plugins", "SpineGpuSkinning");
            Directory.CreateDirectory(Path.Combine(source, "Runtime"));
            Directory.CreateDirectory(Path.Combine(source, "Skills~", "gpuspine-use-plugin"));
            File.WriteAllText(Path.Combine(source, "README.md"), "plugin");
            File.WriteAllText(Path.Combine(source, "Skills~", "gpuspine-use-plugin", "SKILL.md"), "skill");
            SkillInstaller.CopySkill(source, dest, extraSkip: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Skills~" });
            SkillInstaller.WriteMarker(dest, new InstallMarker { Id = "spine-gpu-skinning", Repo = "https://github.com/MlsMoon/SpineGpuSkinning", Commit = "abc" }, SkillInstaller.PluginMarkerFileName);
            Assert.True(File.Exists(Path.Combine(dest, "README.md")));
            Assert.True(Directory.Exists(Path.Combine(dest, "Runtime")));
            Assert.False(Directory.Exists(Path.Combine(dest, "Skills~")));
            Assert.Equal("spine-gpu-skinning", SkillInstaller.ReadMarker(Path.Combine(dest, SkillInstaller.PluginMarkerFileName))?.Id);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void ResolvePluginDestination_RejectsEscape()
    {
        var workspace = CreateTempDir();
        try
        {
            var plugin = new SkillDefinition
            {
                Id = "bad",
                Name = "bad",
                Kind = ToolKind.Plugin,
                InstallPath = "..\\outside"
            };
            Assert.Throws<InvalidOperationException>(() =>
                SkillInstaller.ResolvePluginDestination(workspace, plugin));
        }
        finally
        {
            TryDelete(workspace);
        }
    }

    [Fact]
    public void WorkspaceScanner_InspectsPluginPathNotSkillRoots()
    {
        var workspace = CreateTempDir();
        try
        {
            var dest = Path.Combine(workspace, "Assets", "Plugins", "SpineGpuSkinning");
            Directory.CreateDirectory(dest);
            SkillInstaller.WriteMarker(dest, new InstallMarker { Id = "spine-gpu-skinning", Commit = "deadbeef" }, SkillInstaller.PluginMarkerFileName);
            var plugin = new SkillDefinition
            {
                Id = "spine-gpu-skinning",
                Name = "SpineGpuSkinning",
                Kind = ToolKind.Plugin,
                InstallName = "SpineGpuSkinning",
                InstallPath = "Assets/Plugins/SpineGpuSkinning"
            };
            var status = Assert.Single(new WorkspaceScanner().InspectInstalls(workspace, plugin, [".agent"]));
            Assert.Equal(Path.Combine("Assets", "Plugins", "SpineGpuSkinning"), status.Root);
            Assert.True(status.Installed);
            Assert.True(status.Managed);
            Assert.Equal("deadbeef", status.Commit);
        }
        finally
        {
            TryDelete(workspace);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "msm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
    }
}
