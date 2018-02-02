using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;

namespace Spi.Native
{
    public class PrivilegienStadl
    {
        public static bool TryToSetBackupPrivilege()
        {
            return
                TryToSetPrivilege(
                    szPrivilege:        "SeBackupPrivilege", 
                    bEnablePrivilege:   true);
        }
        //-------------------------------------------------------------------------------------------------
        public static bool TryToSetPrivilege(string szPrivilege, bool bEnablePrivilege)
        {
        //-------------------------------------------------------------------------------------------------

            bool fSuccess = false;
            IntPtr hToken = IntPtr.Zero;

            try
            {
                TOKEN_PRIVILEGE prevState = new TOKEN_PRIVILEGE();
                uint returnLength = 0;

                TOKEN_PRIVILEGE privilegesToGet;
                privilegesToGet.PrivilegeCount = 1;
                privilegesToGet.Privileges.Luid.LowPart = 0;
                privilegesToGet.Privileges.Luid.HighPart = 0;
                privilegesToGet.Privileges.Attributes = bEnablePrivilege ? SE_PRIVILEGE_ENABLED : 0;

                if (!OpenProcessToken(   ProcessToken:  System.Diagnostics.Process.GetCurrentProcess().Handle            //GetCurrentProcess()
                                       , DesiredAccess: TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges
                                       , TokenHandle:   ref hToken))
                {
                    ThrowWin32Exception("OpenProcessToken");
                }
                else if (!LookupPrivilegeValue(     lpSystemName:   null
                                                ,   lpName:         szPrivilege
                                                ,   Luid:           ref privilegesToGet.Privileges.Luid))
                {
                    ThrowWin32Exception("LookupPrivilegeValue");
                }
                else if (!AdjustTokenPrivileges(   TokenHandle:                     hToken
                                                 , DisableAllPrivileges:            false
                                                 , NewState:                ref     privilegesToGet
                                                 , BufferLength:            (uint)System.Runtime.InteropServices.Marshal.SizeOf(privilegesToGet)
                                                 , PreviousState:           ref     prevState
                                                 , ReturnLength:            ref     returnLength))
                {
                    ThrowWin32Exception("AdjustTokenPrivileges");
                }
                else if (System.Runtime.InteropServices.Marshal.GetLastWin32Error() == ERROR_NOT_ALL_ASSIGNED)
                {
                    ThrowWin32Exception("AdjustTokenPrivileges");
                }
                else
                {
                    fSuccess = true;
                } /* endif */
            }
            finally
            {
                if (hToken != IntPtr.Zero)
                {
                    CloseHandle(hToken);
                }
            }

            return fSuccess;
        }

        private static void ThrowWin32Exception(string Win32Apiname)
        {
            var winEx = new Win32Exception();
            throw new Exception($"E-TryToSetPrivilege: Win32Api: {Win32Apiname} ErrorCode: {winEx.ErrorCode}"
                        + $" NativeErrorCode: {winEx.NativeErrorCode} Message: {winEx.Message}", winEx);

        }

        [Flags]
        internal enum TokenAccessLevels
        {
            AssignPrimary = 0x00000001,
            Duplicate = 0x00000002,
            Impersonate = 0x00000004,
            Query = 0x00000008,
            QuerySource = 0x00000010,
            AdjustPrivileges = 0x00000020,
            AdjustGroups = 0x00000040,
            AdjustDefault = 0x00000080,
            AdjustSessionId = 0x00000100,

            Read = 0x00020000 | Query,

            Write = 0x00020000 | AdjustPrivileges | AdjustGroups | AdjustDefault,

            AllAccess = 0x000F0000 |
                AssignPrimary |
                Duplicate |
                Impersonate |
                Query |
                QuerySource |
                AdjustPrivileges |
                AdjustGroups |
                AdjustDefault |
                AdjustSessionId,

            MaximumAllowed = 0x02000000
        }

        internal enum SecurityImpersonationLevel
        {
            Anonymous = 0,
            Identification = 1,
            Impersonation = 2,
            Delegation = 3,
        }

        internal enum TokenType
        {
            Primary = 1,
            Impersonation = 2,
        }

        internal const uint SE_PRIVILEGE_DISABLED = 0x00000000;
        internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LUID
        {
            internal uint LowPart;
            internal uint HighPart;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LUID_AND_ATTRIBUTES
        {
            internal LUID Luid;
            internal uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct TOKEN_PRIVILEGE
        {
            internal uint PrivilegeCount;
            internal LUID_AND_ATTRIBUTES Privileges;
        }

        internal const string ADVAPI32 = "advapi32.dll";
        internal const string KERNEL32 = "kernel32.dll";

        internal const int ERROR_SUCCESS = 0x0;
        internal const int ERROR_ACCESS_DENIED = 0x5;
        internal const int ERROR_NOT_ENOUGH_MEMORY = 0x8;
        internal const int ERROR_NO_TOKEN = 0x3f0;
        internal const int ERROR_NOT_ALL_ASSIGNED = 0x514;
        internal const int ERROR_NO_SUCH_PRIVILEGE = 0x521;
        internal const int ERROR_CANT_OPEN_ANONYMOUS = 0x543;

        [DllImport(KERNEL32,SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport(ADVAPI32,CharSet = CharSet.Unicode,SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static extern bool AdjustTokenPrivileges(
            [In]      IntPtr TokenHandle,
            [In]      bool DisableAllPrivileges,
            [In]      ref TOKEN_PRIVILEGE NewState,
            [In]      uint BufferLength,
            [In, Out] ref TOKEN_PRIVILEGE PreviousState,
            [In, Out] ref uint ReturnLength);

        [DllImport(ADVAPI32,EntryPoint = "LookupPrivilegeValueW",CharSet = CharSet.Unicode,SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static extern bool LookupPrivilegeValue(
            [In]     string lpSystemName,
            [In]     string lpName,
            [In, Out] ref LUID Luid);

        [DllImport(ADVAPI32,CharSet = CharSet.Unicode, SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static extern bool OpenProcessToken(
            [In]     IntPtr ProcessToken,
            [In]     TokenAccessLevels DesiredAccess,
            [In, Out] ref IntPtr TokenHandle);
    }
}