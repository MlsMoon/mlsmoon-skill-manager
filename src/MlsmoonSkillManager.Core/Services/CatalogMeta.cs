using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed record CatalogMetaText(string Name, string Description);

public static class CatalogMeta
{
    public const int ExcerptChars = 200;

    public static string FilePath(SkillDefinition skill)
    {
        if (!skill.IsProjectCopy)
        {
            return Combine(skill.ResolvedSourcePath, "SKILL.md");
        }

        return string.IsNullOrWhiteSpace(skill.ReadmePath) ? "Readme.md" : skill.ReadmePath.Trim();
    }

    public static string Combine(string sourcePath, string file)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath is "." or "./")
        {
            return file;
        }

        return sourcePath.Replace('\\', '/').TrimEnd('/') + "/" + file;
    }

    public static CatalogMetaText Parse(string markdown)
    {
        var map = ReadFrontmatter(markdown);
        map.TryGetValue("name", out var name);
        map.TryGetValue("description", out var description);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = FirstHeading(markdown);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = ExcerptBody(markdown);
        }
        else
        {
            description = Excerpt(description);
        }

        return new CatalogMetaText(name.Trim(), description.Trim());
    }

    public static string Excerpt(string text, int maxChars = ExcerptChars)
    {
        var one = Collapse(text);
        if (one.Length == 0)
        {
            return "";
        }

        var sentence = one.IndexOf(". ", StringComparison.Ordinal);
        if (sentence >= 24 && sentence < maxChars)
        {
            return one[..(sentence + 1)];
        }

        return one.Length <= maxChars ? one : one[..maxChars].TrimEnd() + "…";
    }

    public static bool IsReadmeName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("README.md", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Readme.md", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> LocalCandidates(string file)
    {
        yield return file;
        if (!IsReadmeName(file) || file.Contains('/', StringComparison.Ordinal)
            || file.Contains('\\', StringComparison.Ordinal))
        {
            yield break;
        }

        foreach (var name in new[] { "README.md", "Readme.md", "readme.md" })
        {
            if (!name.Equals(file, StringComparison.OrdinalIgnoreCase))
            {
                yield return name;
            }
        }
    }

    internal static Dictionary<string, string> ReadFrontmatter(string markdown)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return map;
        }

        var lines = markdown.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return map;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---")
            {
                break;
            }

            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var raw = line[(colon + 1)..].Trim();
            if (raw is ">" or ">|" or "|" or ">-" or "|-" or ">+")
            {
                var folded = raw.StartsWith('>');
                var parts = new List<string>();
                while (i + 1 < lines.Length)
                {
                    var next = lines[i + 1];
                    if (next.Trim() == "---")
                    {
                        break;
                    }

                    if (next.Length == 0)
                    {
                        parts.Add("");
                        i++;
                        continue;
                    }

                    if (!char.IsWhiteSpace(next[0]))
                    {
                        break;
                    }

                    parts.Add(next.Trim());
                    i++;
                }

                raw = folded
                    ? string.Join(" ", parts.Where(part => part.Length > 0))
                    : string.Join("\n", parts);
            }
            else
            {
                raw = Unquote(raw);
            }

            if (key.Length > 0)
            {
                map[key] = raw;
            }
        }

        return map;
    }

    private static string FirstHeading(string markdown)
    {
        foreach (var raw in markdown.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line.TrimStart('#').Trim().Trim('*');
            }
        }

        return "";
    }

    private static string ExcerptBody(string markdown)
    {
        var parts = new List<string>();
        var inFront = markdown.StartsWith("---", StringComparison.Ordinal);
        var frontDone = !inFront;
        foreach (var raw in markdown.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (!frontDone)
            {
                if (line == "---" && parts.Count == 0 && inFront)
                {
                    inFront = false;
                    continue;
                }

                if (line == "---")
                {
                    frontDone = true;
                }

                continue;
            }

            if (line.Length == 0)
            {
                if (parts.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (line.StartsWith('#') || IsNavLine(line) || line.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            parts.Add(line.TrimStart('>', '*').Trim());
            if (string.Join(" ", parts).Length >= ExcerptChars)
            {
                break;
            }
        }

        return Excerpt(string.Join(" ", parts));
    }

    private static bool IsNavLine(string line)
    {
        return line.Contains('|', StringComparison.Ordinal)
               && (line.Contains("English", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("中文", StringComparison.Ordinal)
                   || line.Contains(".md", StringComparison.OrdinalIgnoreCase));
    }

    private static string Collapse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var chars = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        while (chars.Contains("  ", StringComparison.Ordinal))
        {
            chars = chars.Replace("  ", " ", StringComparison.Ordinal);
        }

        return chars.Trim();
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
