using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using System.Threading.Tasks;

namespace Vault;

public static class CryptoEngine
{
    private const int MemorySizeKb = 1048576;
    private const int Iterations = 25;
    private const int DegreeOfParallelism = 4;
    
    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = DegreeOfParallelism;
        argon2.MemorySize = MemorySizeKb;
        argon2.Iterations = Iterations;

        return argon2.GetBytes(32);
    }
    
    public static byte[] EncryptString(string plainText, string password)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveKey(password, salt);
        
        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[16];
        
        using var aesGcm = new AesGcm(key, tagSizeInBytes: 16);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
        
        var result = new byte[salt.Length + nonce.Length + tag.Length + cipherText.Length];
        
        Buffer.BlockCopy(salt, 0, result, 0, 16);
        Buffer.BlockCopy(nonce, 0, result, 16, 12);
        Buffer.BlockCopy(tag, 0, result, 28, 16);
        Buffer.BlockCopy(cipherText, 0, result, 44, cipherText.Length);

        return result;
    }
    
    public static string DecryptString(byte[] encryptedData, string password)
    {
        if (encryptedData.Length < 44) throw new CryptographicException("Geçersiz veya bozuk şifreli veri.");
        
        var salt = new byte[16];
        var nonce = new byte[12];
        var tag = new byte[16];
        var cipherText = new byte[encryptedData.Length - 44];

        Buffer.BlockCopy(encryptedData, 0, salt, 0, 16);
        Buffer.BlockCopy(encryptedData, 16, nonce, 0, 12);
        Buffer.BlockCopy(encryptedData, 28, tag, 0, 16);
        Buffer.BlockCopy(encryptedData, 44, cipherText, 0, cipherText.Length);
        
        var key = DeriveKey(password, salt);
        var plainBytes = new byte[cipherText.Length];

        using var aesGcm = new AesGcm(key, tagSizeInBytes: 16);
        aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
    
    public static async Task EncryptFileStreamAsync(string sourcePath, string destinationPath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        using var encryptor = aes.CreateEncryptor();
        await using var cryptoStream = new CryptoStream(destStream, encryptor, CryptoStreamMode.Write);
        
        await sourceStream.CopyToAsync(cryptoStream);
    }
    
    public static async Task DecryptFileStreamAsync(string encryptedSourcePath, string destinationPath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;

        await using var sourceStream = new FileStream(encryptedSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        using var decryptor = aes.CreateDecryptor();
        await using var cryptoStream = new CryptoStream(sourceStream, decryptor, CryptoStreamMode.Read);

        await cryptoStream.CopyToAsync(destStream);
    }
}