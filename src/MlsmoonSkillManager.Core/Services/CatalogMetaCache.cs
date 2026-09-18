using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class CatalogMetaEntry
{
    public string Id { get; set; } = "";
    public string Repo { get; set; } = "";
    public string File { get; set; } = "";
    public string Commit { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class CatalogMetaFile
{
    public int Version { get; set; } = 1;
    public Dictionary<string, CatalogMetaEntry> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CatalogMetaCache
{
    private readonly string _path;
    private CatalogMetaFile _file;

    public CatalogMetaCache(AppPaths paths)
    {
        _path = Path.Combine(paths.CacheDirectory, "catalog-meta.json");
        _file = Read(_path);
    }

    public CatalogMetaEntry? Find(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? null
            : _file.Items.TryGetValue(id, out var item) ? item : null;
    }

    public bool Matches(CatalogMetaEntry entry, SkillDefinition skill, string file, string? commit)
    {
        if (!entry.Id.Equals(skill.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!entry.Repo.Equals(skill.Repo ?? "", StringComparison.OrdinalIgnoreCase)
            && !entry.Repo.Equals(skill.Host + skill.GitPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!entry.File.Equals(file, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(commit)
               || entry.Commit.Equals(commit, StringComparison.OrdinalIgnoreCase);
    }

    public void Upsert(CatalogMetaEntry entry)
    {
        _file.Items[entry.Id] = entry;
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_file, JsonUtil.Options));
    }

    private static CatalogMetaFile Read(string path)
    {
        if (!File.Exists(path))
        {
            return new CatalogMetaFile();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CatalogMetaFile>(File.ReadAllText(path), JsonUtil.Options);
            if (parsed is null)
            {
                return new CatalogMetaFile();
            }

            parsed.Items = new Dictionary<string, CatalogMetaEntry>(
                parsed.Items,
                StringComparer.OrdinalIgnoreCase);
            return parsed;
        }
        catch (JsonException)
        {
            return new CatalogMetaFile();
        }
        catch (IOException)
        {
            return new CatalogMetaFile();
        }
    }
}
