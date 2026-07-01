using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vault.Models;

namespace Vault;

public partial class PasswordVaultWindow : Window
{
    private SecureString _vaultPassword = null!;
    private readonly string _initialJsonData = null!;
    private readonly FileEngine _fileEngine = null!;

    private List<PasswordEntry> _allEntries = [];
    private List<PasswordEntry> _filteredEntries = [];

    public PasswordVaultWindow() => InitializeComponent();

    public PasswordVaultWindow(SecureString vaultPassword, string decryptedJson, FileEngine fileEngine) : this()
    {
        _vaultPassword   = vaultPassword;
        _initialJsonData = decryptedJson;
        _fileEngine      = fileEngine;
        LoadPasswordEntries();
    }

    private void LoadPasswordEntries()
    {
        try
        {
            var cleanJson = _initialJsonData.Trim('\0', ' ', '\r', '\n');
            _allEntries = cleanJson.Length > 2
                ? JsonSerializer.Deserialize<List<PasswordEntry>>(cleanJson)
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
                x.Site.Contains(searchText, StringComparison.OrdinalIgnoreCase)     ||
                x.Email.Contains(searchText, StringComparison.OrdinalIgnoreCase)    ||
                x.Username.Contains(searchText, StringComparison.OrdinalIgnoreCase))];

        LstPasswords.ItemsSource = new ObservableCollection<PasswordEntry>(_filteredEntries);
    }

    private async void OnAddNewClick(object sender, RoutedEventArgs e)
    {
        var addWindow = new PasswordEditWindow();
        var newEntry  = await addWindow.ShowDialog<PasswordEntry?>(this);
        if (newEntry is null) return;

        newEntry.Id = _allEntries.Count > 0 ? _allEntries.Max(x => x.Id) + 1 : 1;
        _allEntries.Add(newEntry);
        ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);

        await SaveAsync();
    }

    private async void OnCopyPasswordClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PasswordEntry entry }) return;
        if (string.IsNullOrEmpty(entry.Password)) return;

        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(entry.Password);
    }

    private async void OnEditPasswordEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PasswordEntry selectedEntry }) return;

        var editWindow = new PasswordEditWindow(selectedEntry);
        var result     = await editWindow.ShowDialog<PasswordEntry?>(this);
        if (result is null) return;

        var index = _allEntries.FindIndex(x => x.Id == selectedEntry.Id);
        if (index != -1)
            _allEntries[index] = result;

        ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);

        await SaveAsync();
    }
    
    private async Task SaveAsync()
    {
        try
        {
            LblSaving.Text = "Kaydediliyor, lütfen bekleyin...";
            SetLoadingState(true);
            await _fileEngine.UpdatePasswordsFile(_allEntries, _vaultPassword);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveAsync error]: {ex.Message}");
            TxtSaveError.Text      = "Kaydedilirken bir hata oluştu.";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Şifreleri JSON Olarak Dışa Aktar",
            SuggestedFileName = $"vault-export-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON dosyası") { Patterns = ["*.json"] }
            ]
        });

        if (file is null) return;

        try
        {
            LblSaving.Text = "Dışa aktarılıyor, lütfen bekleyin...";
            SetLoadingState(true);

            var json = JsonSerializer.Serialize(_allEntries, new JsonSerializerOptions { WriteIndented = true });

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            await writer.FlushAsync();

            TxtExportStatus.Text      = "Dışa aktarma tamamlandı.";
            TxtExportStatus.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Export error]: {ex.Message}");
            TxtSaveError.Text      = "Dışa aktarma sırasında bir hata oluştu.";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "JSON Dosyasından İçe Aktar",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON dosyası") { Patterns = ["*.json"] }
            ]
        });

        if (files.Count == 0) return;

        TxtSaveError.IsVisible    = false;
        TxtExportStatus.IsVisible = false;

        try
        {
            LblSaving.Text = "İçe aktarılıyor, lütfen bekleyin...";
            SetLoadingState(true);

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var importedEntries = JsonSerializer.Deserialize<List<PasswordEntry>>(json);
            if (importedEntries is null || importedEntries.Count == 0)
            {
                TxtSaveError.Text      = "Dosyada içe aktarılacak geçerli bir kayıt bulunamadı.";
                TxtSaveError.IsVisible = true;
                return;
            }

            var nextId = _allEntries.Count > 0 ? _allEntries.Max(x => x.Id) + 1 : 1;
            foreach (var entry in importedEntries)
            {
                entry.Id = nextId++;
                _allEntries.Add(entry);
            }

            ApplyFilter(TxtSearch.Text?.Trim() ?? string.Empty);

            await _fileEngine.UpdatePasswordsFile(_allEntries, _vaultPassword);

            TxtExportStatus.Text      = $"{importedEntries.Count} kayıt başarıyla içe aktarıldı.";
            TxtExportStatus.IsVisible = true;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Import JSON error]: {ex.Message}");
            TxtSaveError.Text      = "Dosya geçerli bir JSON formatında değil.";
            TxtSaveError.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Import error]: {ex.Message}");
            TxtSaveError.Text      = "İçe aktarma sırasında bir hata oluştu.";
            TxtSaveError.IsVisible = true;
        }
        finally
        {
            SetLoadingState(false);
        }
    }
    
    private async void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var changeWindow = new ChangeVaultPasswordWindow();
        var newPassword  = await changeWindow.ShowDialog<SecureString?>(this);
        if (newPassword is null) return;

        TxtSaveError.IsVisible    = false;
        TxtExportStatus.IsVisible = false;

        try
        {
            LblSaving.Text = "Kasa şifresi değiştiriliyor, lütfen bekleyin...";
            SetLoadingState(true);

            await _fileEngine.UpdatePasswordsFile(_allEntries, newPassword);

            _vaultPassword.Dispose();
            _vaultPassword = newPassword;

            TxtExportStatus.Text      = "Kasa şifresi başarıyla değiştirildi.";
            TxtExportStatus.IsVisible = true;
        }
        catch (Exception ex)
        {
            newPassword.Dispose();
            System.Diagnostics.Debug.WriteLine($"[ChangePassword error]: {ex.Message}");
            TxtSaveError.Text      = "Şifre değiştirilirken bir hata oluştu.";
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

    private void SetLoadingState(bool isLoading)
    {
        PgrSaving.IsVisible    = isLoading;
        LblSaving.IsVisible    = isLoading;
        TxtSaveError.IsVisible = !isLoading && TxtSaveError.IsVisible;
        BtnAddNew.IsEnabled    = !isLoading;
        BtnFilter.IsEnabled    = !isLoading;
        BtnImport.IsEnabled          = !isLoading;
        BtnChangePassword.IsEnabled  = !isLoading;
        LstPasswords.IsEnabled = !isLoading;
    }
}