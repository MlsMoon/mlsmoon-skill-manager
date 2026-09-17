using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SettingsStore
{
    private readonly AppPaths _paths;

    public SettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public UserSettings Load()
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            return new UserSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<UserSettings>(
                File.ReadAllText(_paths.SettingsPath),
                JsonUtil.Options) ?? new UserSettings();
            if (settings.SelectedRoots.Count == 0)
            {
                settings.SelectedRoots.Add(SkillRoots.DefaultRoot);
            }

            settings.Theme = ThemeResolver.Normalize(settings.Theme);
            WorkspaceBook.Migrate(settings);
            return settings;
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        _paths.EnsureWritable();
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonUtil.Options));
    }
}
