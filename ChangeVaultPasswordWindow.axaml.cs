using System.Security;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vault;

public partial class ChangeVaultPasswordWindow : Window
{
    public ChangeVaultPasswordWindow() => InitializeComponent();

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var newPassword     = TxtNewPassword.Text ?? string.Empty;
        var confirmPassword = TxtConfirmPassword.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            ShowError("Şifre en az 4 karakter olmalıdır.");
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("Girilen şifreler eşleşmiyor.");
            return;
        }

        var secureString = new SecureString();
        foreach (var c in newPassword)
            secureString.AppendChar(c);
        secureString.MakeReadOnly();
        
        TxtNewPassword.Text     = string.Empty;
        TxtConfirmPassword.Text = string.Empty;

        Close(secureString);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close(null);

    private void ShowError(string message)
    {
        TxtError.Text      = message;
        TxtError.IsVisible = true;
    }
}