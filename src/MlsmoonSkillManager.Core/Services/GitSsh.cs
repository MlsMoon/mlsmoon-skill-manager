using System.Text;

namespace MlsmoonSkillManager.Core.Services;

public static class GitSsh
{
    private const string BatchSsh =
        "ssh -o BatchMode=yes -o ConnectTimeout=5 -o StrictHostKeyChecking=accept-new";

    private const string PasswordSsh =
        "ssh -o BatchMode=no -o PreferredAuthentications=password,keyboard-interactive -o PubkeyAuthentication=no -o NumberOfPasswordPrompts=1 -o ConnectTimeout=5 -o StrictHostKeyChecking=accept-new";

    public static IReadOnlyDictionary<string, string> Variables()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_TERMINAL_PROMPT"] = "0"
        };

        if (!NasCredentials.TryRead(out var secret))
        {
            env["GIT_SSH_COMMAND"] = BatchSsh;
            return env;
        }

        var askpass = WriteAskpass(secret.Password);
        env["GIT_SSH_COMMAND"] = PasswordSsh;
        env["SSH_ASKPASS"] = askpass;
        env["GIT_ASKPASS"] = askpass;
        env["SSH_ASKPASS_REQUIRE"] = "force";
        env["DISPLAY"] = "127.0.0.1:0";
        return env;
    }

    public static bool HasLogin => NasCredentials.TryRead(out _);

    public static void ClearAskpass()
    {
        TryDelete(AskpassCmdPath);
        TryDelete(AskpassDataPath);
    }

    private static string WriteAskpass(string password)
    {
        Directory.CreateDirectory(AskpassDirectory);
        System.IO.File.WriteAllBytes(AskpassDataPath, Encoding.UTF8.GetBytes(password));
        var cmd = "@echo off" + Environment.NewLine + "type \"" + AskpassDataPath + "\"" + Environment.NewLine;
        System.IO.File.WriteAllText(AskpassCmdPath, cmd, Encoding.ASCII);
        return AskpassCmdPath;
    }

    private static string AskpassDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MlsmoonSkillManager");

    private static string AskpassCmdPath => Path.Combine(AskpassDirectory, "ssh-askpass.cmd");

    private static string AskpassDataPath => Path.Combine(AskpassDirectory, "ssh-askpass.dat");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
