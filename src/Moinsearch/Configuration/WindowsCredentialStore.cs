using System.Runtime.InteropServices;
using System.Text;

namespace Moinsearch.Configuration;

internal sealed class WindowsCredentialStore : ICredentialStore
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobSize = 5120;

    public string? Read(Uri wikiUrl)
    {
        EnsureWindows();

        var targetName = Marshal.StringToCoTaskMemUni(GetTargetName(wikiUrl));
        try
        {
            if (CredRead(targetName, GenericCredential, 0, out var credentialPointer) == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error == ErrorNotFound)
                {
                    return null;
                }

                throw new CredentialStoreException(
                    $"Windows Credential Managerから認証情報を読み込めませんでした (Win32 error {error})。");
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlobSize > MaximumCredentialBlobSize ||
                    credential.CredentialBlobSize % sizeof(char) != 0 ||
                    (credential.CredentialBlobSize > 0 && credential.CredentialBlob == IntPtr.Zero))
                {
                    throw new CredentialStoreException("Windows Credential Managerの認証情報の形式が不正です。");
                }

                return credential.CredentialBlobSize == 0
                    ? string.Empty
                    : Marshal.PtrToStringUni(
                        credential.CredentialBlob,
                        checked((int)(credential.CredentialBlobSize / sizeof(char))));
            }
            finally
            {
                CredFree(credentialPointer);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(targetName);
        }
    }

    public void Write(Uri wikiUrl, string password)
    {
        EnsureWindows();

        var passwordSize = Encoding.Unicode.GetByteCount(password);
        if (passwordSize > MaximumCredentialBlobSize)
        {
            throw new CredentialStoreException("パスワードがWindows Credential Managerの保存上限を超えています。");
        }

        var targetName = Marshal.StringToCoTaskMemUni(GetTargetName(wikiUrl));
        var passwordPointer = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = targetName,
                CredentialBlobSize = (uint)passwordSize,
                CredentialBlob = passwordPointer,
                Persist = PersistLocalMachine,
            };

            if (CredWrite(ref credential, 0) == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                throw new CredentialStoreException(
                    $"Windows Credential Managerに認証情報を保存できませんでした (Win32 error {error})。");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(passwordPointer);
            Marshal.FreeCoTaskMem(targetName);
        }
    }

    private static string GetTargetName(Uri wikiUrl) => $"moinsearch:{wikiUrl.AbsoluteUri}";

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential ManagerはWindowsでのみ使用できます。");
        }
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", SetLastError = true)]
    private static extern int CredRead(IntPtr targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    private static extern int CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
