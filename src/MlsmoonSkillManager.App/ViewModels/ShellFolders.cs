using System.Diagnostics;
using System.IO;
using System.Windows;
using MlsmoonSkillManager.App;

namespace MlsmoonSkillManager.App.ViewModels;

public static class ShellFolders
{
    public static void Open(string? path, bool create, string probeDirectory, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            log("没有可打开的路径");
            return;
        }

        try
        {
            if (create && !Directory.Exists(path) && !File.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                log($"找不到 {path}");
                return;
            }

            log($"已打开 {path}");
            var uiTest = Application.Current is App app && app.IsUiTest;
            if (uiTest)
            {
                Directory.CreateDirectory(probeDirectory);
                File.AppendAllText(
                    Path.Combine(probeDirectory, "ui-probe.log"),
                    "open-folder\t" + path + Environment.NewLine);
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log($"无法打开 {path}: {ex.Message}");
        }
    }
}
