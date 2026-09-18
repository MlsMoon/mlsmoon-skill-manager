using System.Runtime.InteropServices;
using System.Text;

namespace MlsmoonSkillManager.Core.Services;

public sealed record NasSecret(string User, string Password);

public static class NasCredentials
{
    public const string Target = "MlsmoonSkillManager:nas";

    public static bool TryRead(out NasSecret secret)
    {
        secret = new NasSecret("", "");
        if (!CredRead(Target, CredTypeGeneric, 0, out var ptr) || ptr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var cred = Marshal.PtrToStructure<NativeCredential>(ptr);
            var user = cred.UserName == IntPtr.Zero ? "" : Marshal.PtrToStringUni(cred.UserName) ?? "";
            var password = "";
            if (cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0)
            {
                var bytes = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
                password = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            }

            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            secret = new NasSecret(user.Trim(), password);
            return true;
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public static void Save(string user, string password)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("NAS 用户名和密码都不能为空。");
        }

        var blob = Encoding.Unicode.GetBytes(password);
        var cred = new NativeCredential
        {
            Type = CredTypeGeneric,
            Persist = CredPersistLocalMachine,
            UserName = Marshal.StringToCoTaskMemUni(user.Trim()),
            TargetName = Marshal.StringToCoTaskMemUni(Target),
            CredentialBlobSize = blob.Length,
            CredentialBlob = Marshal.AllocCoTaskMem(blob.Length)
        };
        Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);
        try
        {
            if (!CredWrite(ref cred, 0))
            {
                throw new InvalidOperationException("无法写入 Windows 凭据管理器（错误 " + Marshal.GetLastWin32Error() + "）。");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(cred.UserName);
            Marshal.FreeCoTaskMem(cred.TargetName);
            Marshal.FreeCoTaskMem(cred.CredentialBlob);
        }
    }

    public static void Delete()
    {
        CredDelete(Target, CredTypeGeneric, 0);
    }

    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
