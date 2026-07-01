using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vault.Helpers;
using Vault.Models;

namespace Vault;

public partial class FileVaultWindow : Window
{
    private SecureString _vaultPassword = null!;
    private readonly string _initialJsonData = null!;
    private readonly FileEngine _fileEngine = null!;

    private List<FileLookupEntry> _allEntries = [];
    private List<FileLookupEntry> _filteredEntries = [];

    public FileVaultWindow() => InitializeComponent();

    public FileVaultWindow(SecureString vaultPassword, string decryptedJson, FileEngine fileEngine) : this()
    {
        _vaultPassword   = vaultPassword;
        _initialJsonData = decryptedJson;
        _fileEngine      = fileEngine;
        LoadFileLookupEntries();
    }

    private void LoadFileLookupEntries()
    {
        try
        {
            var cleanJson = _initialJsonData.Trim('\0', ' ', '\r', '\n');
            _allEntries = cleanJson.Length > 2
                ? JsonSerializer.Deserialize<List<FileLookupEntry>>(cleanJson)
                    ?.OrderBy(x => x.Id).ToList() ?? []
                : [];
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JSON parse error]: {ex.Message}");
            _allEntries = [];
        }

        ApplyFilter(string.Empty);
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
        => ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);

    private void ApplyFilter(string searchText)
    {
        _filteredEntries = string.IsNullOrEmpty(searchText)
            ? [.. _allEntries]
            : [.. _allEntries.Where(x =>
                x.OriginalName.Contains(searchText, StringComparison.OrdinalIgnoreCase))];

        LstPasswords.ItemsSource = new ObservableCollection<FileLookupEntry>(_filteredEntries);
    }

    private async void OnAddNewClick(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Kasaya Eklenecek Dosyayı Seçin",
            AllowMultiple = false
        });

        if (files.Count == 0) return;

        var selectedFile = files[0];
        var sourcePath = selectedFile.Path.LocalPath;

        try
        {
            SetLoadingState(true);
            TxtSaveError.IsVisible = false;
            LblProgress.Text = "Kaydediliyor, lütfen bekleyin...";
            
            var fileInfo = new System.IO.FileInfo(sourcePath);
            var originalName = fileInfo.Name;
            var fileSize = (ulong)fileInfo.Length;
            var mimeType = MimeTypeHelper.GetMimeType(originalName);

            var filePassword = CryptoHelper.GenerateSecurePassword();
            
            var nextId = _allEntries.Count > 0 ? _allEntries.Max(x => x.Id) + 1 : 1;

            var encryptedFileName = $"file_{nextId}.dat";
            await _fileEngine.SaveEncryptedFilePayloadAsync(sourcePath, encryptedFileName, filePassword);

            var newEntry = new FileLookupEntry
            {
                Id = nextId,
                OriginalName = originalName,
                FileSize = fileSize,
                MimeType = mimeType,
                Password = filePassword,
                CreatedAt = DateTime.UtcNow
            };

            _allEntries.Add(newEntry);
            _filteredEntries.Add(newEntry);

            await _fileEngine.UpdateFilesLookupFile(_allEntries, _vaultPassword);
            ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dosya Ekleme Hatası]: {ex.Message}");
            TxtSaveError.Text = $"Dosya eklenirken hata oluştu: {ex.Message}";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FileLookupEntry selectedEntry }) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        
        var saveOptions = new FilePickerSaveOptions
        {
            Title = "Dosyayı Şifresiz Olarak Dışarı Aktar",
            SuggestedFileName = selectedEntry.OriginalName
        };

        var targetFile = await topLevel.StorageProvider.SaveFilePickerAsync(saveOptions);
        
        if (targetFile == null) return; 

        var destinationPath = targetFile.Path.LocalPath;

        try
        {
            SetLoadingState(true);
            TxtSaveError.IsVisible = false;
            
            LblProgress.Text = "İndiriliyor, lütfen bekleyin...";

            var encryptedFileName = $"file_{selectedEntry.Id}.dat";

            var filePassword = selectedEntry.Password;
            
            await _fileEngine.DecryptAndExtractFilePayloadAsync(encryptedFileName, destinationPath, filePassword);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dosya İndirme Hatası]: {ex.Message}");
            TxtSaveError.Text = $"Dosya indirilirken bir hata oluştu: {ex.Message}";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FileLookupEntry selectedEntry }) return;
        
        try
        {
            SetLoadingState(true);
            TxtSaveError.IsVisible = false;
            LblProgress.Text = "Siliniyor, lütfen bekleyin...";

            var encryptedFileName = $"file_{selectedEntry.Id}.dat";
            _fileEngine.DeleteFilePayload(encryptedFileName);

            _allEntries.Remove(selectedEntry);

            await _fileEngine.UpdateFilesLookupFile(_allEntries, _vaultPassword);
            ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dosya Silme Hatası]: {ex.Message}");
            TxtSaveError.Text = $"Dosya silinirken bir hata oluştu: {ex.Message}";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        _vaultPassword.Dispose();
        
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }
    
    private async void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var changeWindow = new ChangeVaultPasswordWindow();
        var newPassword  = await changeWindow.ShowDialog<SecureString?>(this);
        if (newPassword is null) return;

        TxtSaveError.IsVisible = false;

        try
        {
            SetLoadingState(true);
            LblProgress.Text = "Kasa şifresi değiştiriliyor, lütfen bekleyin...";

            await _fileEngine.UpdateFilesLookupFile(_allEntries, newPassword);

            _vaultPassword.Dispose();
            _vaultPassword = newPassword;

            LblProgress.Text = "Kasa şifresi başarıyla değiştirildi.";
            LblProgress.IsVisible = true;
        }
        catch (Exception ex)
        {
            newPassword.Dispose();
            System.Diagnostics.Debug.WriteLine($"[ChangePassword error]: {ex.Message}");
            TxtSaveError.Text = $"Şifre değiştirilirken bir hata oluştu: {ex.Message}";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        PgrProgressBar.IsVisible    = isLoading;
        LblProgress.IsVisible    = isLoading;
        TxtSaveError.IsVisible = !isLoading && TxtSaveError.IsVisible;
        BtnAddNew.IsEnabled    = !isLoading;
        BtnFilter.IsEnabled    = !isLoading;
        BtnChangePassword.IsEnabled   = !isLoading;
        LstPasswords.IsEnabled = !isLoading;
    }
}