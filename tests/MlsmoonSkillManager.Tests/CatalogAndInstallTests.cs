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
    public void PluginInstaller_CopiesSkillsTildeAndMlsmoon()
    {
        var root = CreateTempDir();
        try
        {
            var source = Path.Combine(root, "src");
            var dest = Path.Combine(root, "Assets", "Plugins", "SpineGpuSkinning");
            Directory.CreateDirectory(Path.Combine(source, "Runtime"));
            Directory.CreateDirectory(Path.Combine(source, "Skills~", "gpuspine-use-plugin"));
            Directory.CreateDirectory(Path.Combine(source, ".mlsmoon"));
            File.WriteAllText(Path.Combine(source, "README.md"), "plugin");
            File.WriteAllText(Path.Combine(source, "Skills~", "gpuspine-use-plugin", "SKILL.md"), "skill");
            File.WriteAllText(
                Path.Combine(source, ".mlsmoon", "skill.json"),
                """{"id":"spine-gpu-skinning-skill","kind":"routing","parentId":"spine-gpu-skinning"}""");
            SkillInstaller.CopySkill(source, dest, ProjectCopy.SkipNames);
            SkillInstaller.WriteMarker(dest, new InstallMarker { Id = "spine-gpu-skinning", Repo = "https://github.com/MlsMoon/SpineGpuSkinning", Commit = "abc" }, SkillInstaller.PluginMarkerFileName);
            Assert.True(File.Exists(Path.Combine(dest, "README.md")));
            Assert.True(Directory.Exists(Path.Combine(dest, "Runtime")));
            Assert.True(File.Exists(Path.Combine(dest, "Skills~", "gpuspine-use-plugin", "SKILL.md")));
            Assert.True(File.Exists(MlsmoonSkillConfig.FilePath(dest)));
            Assert.Equal("spine-gpu-skinning", SkillInstaller.ReadMarker(Path.Combine(dest, SkillInstaller.PluginMarkerFileName))?.Id);

            var workspace = root;
            Directory.CreateDirectory(Path.Combine(workspace, ".agents", "skills", "renamed-router"));
            MlsmoonSkillConfig.Write(
                Path.Combine(workspace, ".agents", "skills", "renamed-router"),
                new MlsmoonSkillFile
                {
                    Id = "spine-gpu-skinning-skill",
                    Kind = "routing",
                    ParentId = "spine-gpu-skinning"
                });
            File.WriteAllText(Path.Combine(workspace, ".agents", "skills", "renamed-router", "SKILL.md"), "route");
            var routing = MlsmoonSkillConfig.ToRoutingCompanion(
                new SkillDefinition
                {
                    Id = "spine-gpu-skinning",
                    Name = "SpineGpuSkinning",
                    Kind = ToolKind.Plugin,
                    InstallPath = "Assets/Plugins/SpineGpuSkinning"
                },
                MlsmoonSkillConfig.Read(dest)!);
            Assert.Equal(".mlsmoon/project-skill/spine-gpu-skinning-skill", routing.SourcePath);
            var status = Assert.Single(new WorkspaceScanner().InspectInstalls(workspace, routing, [".agents"]));
            Assert.True(status.Installed);
            Assert.Equal(Path.Combine(workspace, ".agents", "skills", "renamed-router"), status.Path);

            var leftover = new SkillDefinition
            {
                Id = "gpuspine-use-plugin",
                Kind = ToolKind.Companion,
                ParentPluginId = "spine-gpu-skinning"
            };
            var plugin = new SkillDefinition
            {
                Id = "spine-gpu-skinning",
                Name = "SpineGpuSkinning",
                Kind = ToolKind.Plugin,
                InstallName = "SpineGpuSkinning",
                InstallPath = "Assets/Plugins/SpineGpuSkinning",
                CompanionSkills = [leftover]
            };
            leftover.ParentPluginId = plugin.Id;
            var packageDest = Path.Combine(workspace, "Packages", "UrpPackageIGP", ".mlsmoon");
            Directory.CreateDirectory(packageDest);
            File.WriteAllText(
                Path.Combine(packageDest, "skill.json"),
                """{"id":"spine-gpu-skinning-skill","kind":"routing","parentId":"spine-gpu-skinning"}""");
            var package = new SkillDefinition
            {
                Id = "urp-package-igp",
                Name = "UrpPackageIGP",
                Kind = ToolKind.Package,
                InstallName = "UrpPackageIGP",
                InstallPath = "Packages/UrpPackageIGP"
            };
            var bound = CatalogRouting.Bind(
                [plugin, leftover, package],
                workspace,
                new AppPaths(root, Path.Combine(root, "config"), Path.Combine(root, "cache")));
            Assert.True(Assert.Single(plugin.CompanionSkills).IsRouting);
            Assert.Equal("spine-gpu-skinning-skill", plugin.CompanionSkills[0].Id);
            Assert.Equal("spine-gpu-skinning", plugin.CompanionSkills[0].ParentPluginId);
            Assert.Empty(package.CompanionSkills);
            Assert.Contains("gpuspine-use-plugin", bound.RemoveIds);
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

    [Fact]
    public void RoutingSkillSource_CopiesAndDiffsAgainstWorkspace()
    {
        var workspace = CreateTempDir();
        try
        {
            var plugin = new SkillDefinition
            {
                Id = "spine-gpu-skinning",
                Name = "SpineGpuSkinning",
                Kind = ToolKind.Plugin,
                InstallName = "SpineGpuSkinning",
                InstallPath = "Assets/Plugins/SpineGpuSkinning"
            };
            var pluginDest = Path.Combine(workspace, "Assets", "Plugins", "SpineGpuSkinning");
            var source = RoutingSkillSource.PluginSourceDirectory(pluginDest, "spine-gpu-skinning-skill");
            Directory.CreateDirectory(Path.Combine(source, "references"));
            File.WriteAllText(Path.Combine(source, "SKILL.md"), "from-source");
            File.WriteAllText(Path.Combine(source, "references", "a.md"), "ref");

            var dest = Path.Combine(workspace, ".agents", "skills", "spine-gpu-skinning-skill");
            Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest, "SKILL.md"), "workspace");
            File.WriteAllText(Path.Combine(dest, "extra.md"), "keep");
            File.WriteAllText(Path.Combine(dest, SkillInstaller.MarkerFileName), "{}");
            MlsmoonSkillConfig.Write(dest, new MlsmoonSkillFile
            {
                Id = "spine-gpu-skinning-skill",
                Kind = "routing",
                ParentId = plugin.Id
            });

            var routing = MlsmoonSkillConfig.ToRoutingCompanion(
                plugin,
                new MlsmoonSkillFile
                {
                    Id = "spine-gpu-skinning-skill",
                    Kind = "routing",
                    ParentId = plugin.Id
                });
            Assert.Equal(".mlsmoon/project-skill/spine-gpu-skinning-skill", routing.SourcePath);

            var align = RoutingSkillSource.Inspect(plugin, routing, workspace, [".agents"]);
            Assert.True(align.SourceExists);
            Assert.True(align.DestExists);
            Assert.Contains(align.Changes, item => item.Path == "SKILL.md" && item.Kind == GitChangeKind.Modified);
            Assert.Contains(align.Changes, item => item.Path == "extra.md" && item.Kind == GitChangeKind.Added);
            Assert.Contains(align.Changes, item => item.Path == "references/a.md" && item.Kind == GitChangeKind.Deleted);
            Assert.DoesNotContain(align.Changes, item => item.Path.Contains(".mlsmoon", StringComparison.OrdinalIgnoreCase));

            RoutingSkillSource.CopyFromSource(plugin, routing, workspace, [".agents"], null);
            Assert.Equal("from-source", File.ReadAllText(Path.Combine(dest, "SKILL.md")));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(dest, "extra.md")));
            Assert.True(File.Exists(Path.Combine(dest, "references", "a.md")));
            Assert.True(File.Exists(Path.Combine(dest, SkillInstaller.MarkerFileName)));
            Assert.True(File.Exists(MlsmoonSkillConfig.FilePath(dest)));

            File.WriteAllText(Path.Combine(dest, "SKILL.md"), "edited");
            var afterToSource = RoutingSkillSource.CopyToSource(plugin, routing, workspace, [".agents"], null);
            Assert.Equal("edited", File.ReadAllText(Path.Combine(source, "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(source, "extra.md")));
            Assert.False(Directory.Exists(Path.Combine(source, MlsmoonSkillConfig.FolderName)));
            Assert.False(File.Exists(Path.Combine(source, SkillInstaller.MarkerFileName)));
            Assert.Empty(afterToSource.Changes);

            File.WriteAllText(Path.Combine(source, "SKILL.md"), "source-v2");
            RoutingSkillInstall.Ensure(
                plugin,
                new MlsmoonSkillFile
                {
                    Id = "spine-gpu-skinning-skill",
                    Kind = "routing",
                    ParentId = plugin.Id
                },
                workspace,
                [".agents"],
                new InstallMarker { Id = plugin.Id, Repo = plugin.Repo, Commit = "abc", Branch = "main" },
                null);
            Assert.Equal("source-v2", File.ReadAllText(Path.Combine(dest, "SKILL.md")));

            RoutingSkillInstall.Ensure(
                plugin,
                new MlsmoonSkillFile
                {
                    Id = "empty-router",
                    Kind = "routing",
                    ParentId = plugin.Id
                },
                workspace,
                [".agents"],
                new InstallMarker { Id = plugin.Id, Commit = "abc" },
                null);
            var generated = File.ReadAllText(Path.Combine(workspace, ".agents", "skills", "empty-router", "SKILL.md"));
            Assert.Contains("本文件由 Moon Game Dev Tool Manager", generated);
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
