using System;
using Microsoft.Win32.SafeHandles;
using System.Security.Permissions;

namespace Spi.Native
{
    public sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // Methods
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private SafeFindHandle() : base(true)
        {
        }

        private SafeFindHandle(IntPtr preExistingHandle, bool ownsHandle) : base(ownsHandle)
        {
            base.SetHandle(preExistingHandle);
        }

        protected override bool ReleaseHandle()
        {
            if (!(IsInvalid || IsClosed))
            {
                return Spi.Native.Win32.FindClose(this);
            }
            return (IsInvalid || IsClosed);
        }

        protected override void Dispose(bool disposing)
        {
            if (!(IsInvalid || IsClosed))
            {
                bool rc = Spi.Native.Win32.FindClose(this);
            }
            base.Dispose(disposing);
        }
    }
}
