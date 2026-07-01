using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using Vault.Helpers;
using Vault.Models;

namespace Vault;

public class FileEngine
{
    private readonly string _rootPath;

    private const string VaultTypePassword = "Password";
    private const string VaultTypeFile     = "File";

    private const string PasswordsFileName   = "passwords.json";
    private const string FilesLookupFileName = "files.json";

    public FileEngine()
    {
        _rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vault");
    }

    // GET

    public bool IsVaultExists(string vaultType) => Directory.Exists(GetVaultPath(vaultType));

    public async Task<string> GetDecryptedPasswords(SecureString password)
    {
        var filePath = GetFilePath(VaultTypePassword, PasswordsFileName);
        string? plain = null;
        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(filePath);
            plain = CryptoHelper.SecureStringToString(password);
            return await Task.Run(() => CryptoEngine.DecryptString(encryptedBytes, plain));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return string.Empty;
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }
    
    public async Task<string> GetDecryptedFilesLookup(SecureString password)
    {
        var filePath = GetFilePath(VaultTypeFile, FilesLookupFileName);
        string? plain = null;
        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(filePath);
            plain = CryptoHelper.SecureStringToString(password);
            return await Task.Run(() => CryptoEngine.DecryptString(encryptedBytes, plain));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return string.Empty;
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }
    
    public async Task DecryptAndExtractFilePayloadAsync(string fileName, string destinationPath, byte[] key, byte[] iv)
    {
        var sourcePath = Path.Combine(GetVaultPath(VaultTypeFile), fileName);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Şifreli kaynak dosya kasada bulunamadı.");
        await CryptoEngine.DecryptFileStreamAsync(sourcePath, destinationPath, key, iv);
    }

    // CREATE

    public async Task CreatePasswordVault(SecureString password)
    {
        Directory.CreateDirectory(GetVaultPath(VaultTypePassword));
        string? plain = null;
        try
        {
            plain = CryptoHelper.SecureStringToString(password);
            var encrypted = await Task.Run(() => CryptoEngine.EncryptString("[]", plain));
            await File.WriteAllBytesAsync(GetFilePath(VaultTypePassword, PasswordsFileName), encrypted);
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }

    public async Task CreateFileVault(SecureString password)
    {
        Directory.CreateDirectory(GetVaultPath(VaultTypeFile));
        string? plain = null;
        try
        {
            plain = CryptoHelper.SecureStringToString(password);
            var encrypted = await Task.Run(() => CryptoEngine.EncryptString("[]", plain));
            await File.WriteAllBytesAsync(GetFilePath(VaultTypeFile, FilesLookupFileName), encrypted);
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }
    
    public async Task SaveEncryptedFilePayloadAsync(string sourcePath, string targetFileName, byte[] key, byte[] iv)
    {
        var vaultDir = GetVaultPath(VaultTypeFile);
        var destinationPath = Path.Combine(vaultDir, targetFileName);
        await CryptoEngine.EncryptFileStreamAsync(sourcePath, destinationPath, key, iv);
    }

    // UPDATE

    public async Task UpdatePasswordsFile(List<PasswordEntry> passwords, SecureString password)
    {
        var targetPath = GetFilePath(VaultTypePassword, PasswordsFileName);
        var plainJson  = JsonSerializer.Serialize(passwords);
        string? plain  = null;
        try
        {
            plain = CryptoHelper.SecureStringToString(password);
            var encrypted = await Task.Run(() => CryptoEngine.EncryptString(plainJson, plain));
            await WriteAtomicAsync(targetPath, encrypted);
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }
    
    public async Task UpdateFilesLookupFile(List<FileLookupEntry> fileLookups, SecureString password)
    {
        var targetPath = GetFilePath(VaultTypeFile, FilesLookupFileName);
        var plainJson  = JsonSerializer.Serialize(fileLookups);
        string? plain  = null;
        try
        {
            plain = CryptoHelper.SecureStringToString(password);
            var encrypted = await Task.Run(() => CryptoEngine.EncryptString(plainJson, plain));
            await WriteAtomicAsync(targetPath, encrypted);
        }
        finally
        {
            if (plain != null) CryptoHelper.BurnString(plain);
        }
    }
    
    // DELETE

    public void DeleteRootPath(bool confirmed)
    {
        if (!confirmed) return;
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
    
    public void DeleteFilePayload(string fileName)
    {
        var filePath = Path.Combine(GetVaultPath(VaultTypeFile), fileName);
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    // PRIVATE

    private string GetVaultPath(string vaultType)
        => Path.Combine(_rootPath, "data", vaultType.ToLowerInvariant());

    private string GetFilePath(string vaultType, string fileName)
        => Path.Combine(GetVaultPath(vaultType), fileName);

    private static async Task WriteAtomicAsync(string targetPath, byte[] data)
    {
        var tempPath = targetPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, data);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw new InvalidOperationException("Kasa dosyası güncellenirken bir hata oluştu.", ex);
        }
    }
}