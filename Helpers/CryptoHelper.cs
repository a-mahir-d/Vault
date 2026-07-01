using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Vault.Helpers;

public static class CryptoHelper
{
    public static string SecureStringToString(SecureString secure)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
            return Marshal.PtrToStringUni(ptr)!;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    public static unsafe void BurnString(string value)
    {
        fixed (char* ptr = value)
            for (var i = 0; i < value.Length; i++)
                ptr[i] = '\0';
    }
}