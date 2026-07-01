using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vault.Models;

namespace Vault;

public partial class PasswordEditWindow : Window
{
    private readonly PasswordEntry? _originalEntry;
    private readonly bool _isEditMode;
    private readonly ObservableCollection<string> _recoveryCodesTarget = [];

    public PasswordEditWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    public PasswordEditWindow(PasswordEntry? entry = null) : this()
    {
        _originalEntry = entry;
        _isEditMode    = entry != null;

        if (_isEditMode && entry != null)
        {
            LblTitle.Text    = $"{entry.Site} Kaydını Düzenle";
            TxtSite.Text     = entry.Site;
            TxtEmail.Text    = entry.Email;
            TxtUsername.Text = entry.Username;
            TxtPassword.Text = entry.Password;

            foreach (var code in entry.RecoveryCodes)
                _recoveryCodesTarget.Add(code);
        }
        else
        {
            LblTitle.Text = "Yeni Şifre Kaydı Ekle";
        }
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        LstRecoveryCodes.ItemsSource = _recoveryCodesTarget;
    }

    private void OnAddCodeClick(object sender, RoutedEventArgs e)
    {
        var newCode = TxtNewCode.Text?.Trim();
        if (string.IsNullOrEmpty(newCode)) return;

        _recoveryCodesTarget.Add(newCode);
        TxtNewCode.Text = string.Empty;
        TxtNewCode.Focus();
    }

    private void OnRemoveCodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string code })
            _recoveryCodesTarget.Remove(code);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close(null);

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var site     = TxtSite.Text?.Trim()     ?? string.Empty;
        var password = TxtPassword.Text?.Trim() ?? string.Empty;

        TxtSite.Classes.Set("error", string.IsNullOrWhiteSpace(site));
        TxtPassword.Classes.Set("error", string.IsNullOrWhiteSpace(password));

        if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(password))
            return;

        var result = new PasswordEntry
        {
            Id            = _isEditMode ? _originalEntry!.Id : 0,
            Site          = site,
            Email         = TxtEmail.Text?.Trim()    ?? string.Empty,
            Username      = TxtUsername.Text?.Trim() ?? string.Empty,
            Password      = password,
            RecoveryCodes = [.. _recoveryCodesTarget]
        };

        Close(result);
    }
}