using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Rasid.Services
{
    /// Minimal wrapper around the Windows Credential Manager API.
    /// Keeps API keys out of plain-text configuration files and avoids a
    /// legacy .NET Framework-only NuGet dependency.
    /// safely persists the API key using Windows Credential Manager.

    internal static class WindowsCredentialStore
    {
        private const uint CredTypeGeneric = 1;
        private const uint CredPersistLocalMachine = 2;
        private const int ErrorNotFound = 1168;

        public static void Save(string target, string userName, string secret)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Credential target is required.", nameof(target));
            if (string.IsNullOrEmpty(secret))
                throw new ArgumentException("Credential value is required.", nameof(secret));

            var blob = Marshal.StringToCoTaskMemUni(secret);
            try
            {
                var credential = new NativeCredential
                {
                    Type = CredTypeGeneric,
                    TargetName = target,
                    CredentialBlobSize = (uint)(secret.Length * sizeof(char)),
                    CredentialBlob = blob,
                    Persist = CredPersistLocalMachine,
                    UserName = userName ?? string.Empty
                };

                if (!CredWrite(ref credential, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        "Could not save the RASID API key in Windows Credential Manager.");
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }

        public static bool TryRead(string target, out string secret)
        {
            secret = null;
            if (!CredRead(target, CredTypeGeneric, 0, out var credentialPointer))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                    return false;

                throw new Win32Exception(error,
                    "Could not read the RASID API key from Windows Credential Manager.");
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                    return false;

                secret = Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / sizeof(char)));
                return !string.IsNullOrEmpty(secret);
            }
            finally
            {
                CredFree(credentialPointer);
            }
        }

        public static void Delete(string target)
        {
            if (CredDelete(target, CredTypeGeneric, 0))
                return;

            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
                throw new Win32Exception(error,
                    "Could not remove the RASID API key from Windows Credential Manager.");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
            public FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(
            string target,
            uint type,
            uint reservedFlag,
            out IntPtr credentialPointer);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", SetLastError = false)]
        private static extern void CredFree(IntPtr buffer);
    }
}
