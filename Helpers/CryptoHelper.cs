using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace Vault.Helpers;

public static class CryptoHelper
{
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{}<>?/.,~";
    private const int PasswordLength = 32;
    
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

    public static string GenerateSecurePassword()
    {
        const string pool = UpperChars + LowerChars + DigitChars + SymbolChars;
        var buffer = new char[PasswordLength];


        var categories = new[] { UpperChars, LowerChars, DigitChars, SymbolChars };

        var position = 0;
        foreach (var category in categories)
        {
            buffer[position++] = category[RandomNumberGenerator.GetInt32(category.Length)];
        }
        
        for (var i = position; i < PasswordLength; i++)
        {
            buffer[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }
        
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}