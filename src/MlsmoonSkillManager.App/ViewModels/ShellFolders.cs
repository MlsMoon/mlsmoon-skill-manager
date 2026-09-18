using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            var full = Path.GetFullPath(path.Trim());
            if (create && !Directory.Exists(full) && !File.Exists(full))
            {
                Directory.CreateDirectory(full);
            }

            if (!File.Exists(full) && !Directory.Exists(full))
            {
                log($"找不到 {full}");
                return;
            }

            if (Application.Current is App app && app.IsUiTest)
            {
                Directory.CreateDirectory(probeDirectory);
                File.AppendAllText(
                    Path.Combine(probeDirectory, "ui-probe.log"),
                    "open-folder\t" + full + Environment.NewLine);
                log($"已打开 {full}");
                return;
            }

            if (Directory.Exists(full))
            {
                OpenDirectory(full);
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = full, UseShellExecute = true });
            }

            log($"已打开 {full}");
        }
        catch (Exception ex)
        {
            log($"无法打开 {path}: {ex.Message}");
        }
    }

    private static void OpenDirectory(string path)
    {
        if (Explore(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
            return;
        }
        catch
        {
        }

        var quoted = "\"" + path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "/n," + quoted,
            UseShellExecute = false
        });
    }

    private static bool Explore(string path)
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null)
            {
                return false;
            }

            var shell = Activator.CreateInstance(type);
            if (shell is null)
            {
                return false;
            }

            type.InvokeMember("Explore", BindingFlags.InvokeMethod, null, shell, [path]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
