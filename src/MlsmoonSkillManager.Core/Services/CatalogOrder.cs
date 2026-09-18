using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class CatalogOrder
{
    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> items, Func<T, SkillDefinition> of)
    {
        var list = items as IList<T> ?? items.ToList();
        var byId = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            var skill = of(item);
            if (!string.IsNullOrWhiteSpace(skill.Id))
            {
                byId[skill.Id] = skill;
            }
        }

        return list
            .Select((item, index) => (item, index, skill: of(item)))
            .OrderBy(entry => Rank(entry.skill, byId))
            .ThenBy(entry => Family(entry.skill), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.skill.IsCompanion ? 1 : 0)
            .ThenBy(entry => entry.skill.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.item)
            .ToList();
    }

    public static int Rank(SkillDefinition skill, IReadOnlyDictionary<string, SkillDefinition> byId)
    {
        if (skill.IsPackage || ParentIs(skill, byId, item => item.IsPackage))
        {
            return 2;
        }

        if (skill.IsPlugin || skill.IsCompanion)
        {
            return 1;
        }

        return 0;
    }

    public static string Family(SkillDefinition skill) =>
        skill.IsCompanion && !string.IsNullOrWhiteSpace(skill.ParentPluginId)
            ? skill.ParentPluginId
            : skill.Id;

    public static bool ParentIs(
        SkillDefinition skill,
        IReadOnlyDictionary<string, SkillDefinition> byId,
        Func<SkillDefinition, bool> match) =>
        skill.IsCompanion
        && !string.IsNullOrWhiteSpace(skill.ParentPluginId)
        && byId.TryGetValue(skill.ParentPluginId, out var parent)
        && match(parent);
}
