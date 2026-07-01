using System;
using System.Buffers.Binary;
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
    
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    
    private const int ChunkSize = 64 * 1024;
    
    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = DegreeOfParallelism;
        argon2.MemorySize = MemorySizeKb;
        argon2.Iterations = Iterations;

        return argon2.GetBytes(32);
    }
    
    private static Task<byte[]> DeriveKeyAsync(string password, byte[] salt) => Task.Run(() => DeriveKey(password, salt));
    
    private static void DeriveChunkNonce(ReadOnlySpan<byte> baseNonce, uint chunkIndex, Span<byte> destination)
    {
        baseNonce.CopyTo(destination);
 
        Span<byte> counterBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counterBytes, chunkIndex);
 
        for (var i = 0; i < 4; i++)
        {
            destination[NonceSize - 4 + i] ^= counterBytes[i];
        }
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
    
    public static async Task EncryptFileAsync(string sourcePath, string destinationPath, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var baseNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = await DeriveKeyAsync(password, salt);

        try
        {
            await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            await destStream.WriteAsync(salt);
            await destStream.WriteAsync(baseNonce);

            using var aesGcm = new AesGcm(key, tagSizeInBytes: TagSize);

            var plainBuffer = new byte[ChunkSize];
            var cipherBuffer = new byte[ChunkSize];
            var tag = new byte[TagSize];
            var nonce = new byte[NonceSize];
            var lengthPrefix = new byte[4];

            uint chunkIndex = 0;
            int bytesRead;

            while ((bytesRead = await ReadFullChunkAsync(sourceStream, plainBuffer)) > 0)
            {
                DeriveChunkNonce(baseNonce, chunkIndex, nonce);

                var plainSpan = plainBuffer.AsSpan(0, bytesRead);
                var cipherSpan = cipherBuffer.AsSpan(0, bytesRead);

                aesGcm.Encrypt(nonce, plainSpan, cipherSpan, tag);

                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, bytesRead);
                await destStream.WriteAsync(lengthPrefix);
                await destStream.WriteAsync(cipherBuffer.AsMemory(0, bytesRead));
                await destStream.WriteAsync(tag);

                checked { chunkIndex++; }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
    
    public static async Task DecryptFileAsync(string encryptedSourcePath, string destinationPath, string password)
    {
        await using var sourceStream = new FileStream(encryptedSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        var salt = new byte[SaltSize];
        var baseNonce = new byte[NonceSize];

        await ReadExactAsync(sourceStream, salt, "Dosya başlığı okunamadı (salt).");
        await ReadExactAsync(sourceStream, baseNonce, "Dosya başlığı okunamadı (nonce).");

        var key = await DeriveKeyAsync(password, salt);
        
        var tempPath = destinationPath + ".tmp";

        try
        {
            await using (var destStream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                using var aesGcm = new AesGcm(key, tagSizeInBytes: TagSize);

                var cipherBuffer = new byte[ChunkSize];
                var plainBuffer = new byte[ChunkSize];
                var tag = new byte[TagSize];
                var nonce = new byte[NonceSize];
                var lengthPrefix = new byte[4];

                uint chunkIndex = 0;

                while (true)
                {
                    var read = await sourceStream.ReadAsync(lengthPrefix.AsMemory(0, 4));
                    if (read == 0)
                    {
                        break;
                    }

                    if (read != 4)
                    {
                        throw new CryptographicException("Şifreli dosya bozuk (uzunluk alanı eksik).");
                    }

                    var chunkLength = BinaryPrimitives.ReadInt32BigEndian(lengthPrefix);
                    if (chunkLength < 0 || chunkLength > ChunkSize)
                    {
                        throw new CryptographicException("Şifreli dosya bozuk (geçersiz chunk uzunluğu).");
                    }

                    await ReadExactAsync(sourceStream, cipherBuffer.AsMemory(0, chunkLength),
                        "Şifreli dosya bozuk (ciphertext eksik).");
                    await ReadExactAsync(sourceStream, tag, "Şifreli dosya bozuk (tag eksik).");

                    DeriveChunkNonce(baseNonce, chunkIndex, nonce);
                    
                    aesGcm.Decrypt(nonce, cipherBuffer.AsSpan(0, chunkLength), tag, plainBuffer.AsSpan(0, chunkLength));

                    await destStream.WriteAsync(plainBuffer.AsMemory(0, chunkLength));

                    checked { chunkIndex++; }
                }
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static async Task<int> ReadFullChunkAsync(Stream stream, byte[] buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, string errorMessage)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..]);
            if (read == 0)
            {
                throw new CryptographicException(errorMessage);
            }

            totalRead += read;
        }
    }
    
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}