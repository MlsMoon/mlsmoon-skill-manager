using System.Text.Json;
using System.Text.Json.Nodes;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class UnityWorkspace
{
    public static string? ReadEditorVersion(string workspacePath)
    {
        var file = Path.Combine(workspacePath, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(file))
        {
            return null;
        }

        foreach (var line in File.ReadLines(file))
        {
            const string prefix = "m_EditorVersion:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }

    public static LanGitBranch? PickBranch(SkillDefinition skill, string? editorVersion)
    {
        if (skill.Branches.Count == 0)
        {
            return null;
        }

        var key = EditorFamily(editorVersion);
        if (key is not null)
        {
            var match = skill.Branches.FirstOrDefault(item =>
                item.Unity.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return skill.Branches.FirstOrDefault(item =>
                   item.Name.Equals("urp-17.5", StringComparison.OrdinalIgnoreCase))
               ?? skill.Branches[0];
    }

    public static void MergeManifest(string workspacePath, IReadOnlyDictionary<string, string> dependencies)
    {
        var file = Path.Combine(workspacePath, "Packages", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var root = File.Exists(file)
            ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject()
            : new JsonObject();
        if (root["dependencies"] is not JsonObject map)
        {
            map = new JsonObject();
            root["dependencies"] = map;
        }

        foreach (var pair in dependencies)
        {
            map[pair.Key] = pair.Value;
        }

        File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void RemoveManifestPrefix(string workspacePath, string valuePrefix)
    {
        var file = Path.Combine(workspacePath, "Packages", "manifest.json");
        if (!File.Exists(file) || JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root)
        {
            return;
        }

        if (root["dependencies"] is not JsonObject map)
        {
            return;
        }

        var remove = map
            .Where(pair => pair.Value?.GetValue<string>()?
                .StartsWith(valuePrefix, StringComparison.OrdinalIgnoreCase) == true)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in remove)
        {
            map.Remove(key);
        }

        File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? EditorFamily(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        if (version.StartsWith("6000", StringComparison.Ordinal) || version.StartsWith("6.", StringComparison.Ordinal))
        {
            return "6000";
        }

        if (version.StartsWith("2022", StringComparison.Ordinal))
        {
            return "2022";
        }

        return null;
    }
}
