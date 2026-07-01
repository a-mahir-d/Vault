using System.Security;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vault;

public partial class MainWindow : Window
{
    private readonly FileEngine _fileEngine = new();

    public MainWindow() => InitializeComponent();

    private async void OnVaultSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clickedButton) return;

        SetStatus(visible: false);

        var vaultType = clickedButton.Name switch
        {
            "BtnPasswordVault" => "Password",
            "BtnFileVault"     => "File",
            _                  => string.Empty
        };
        if (string.IsNullOrEmpty(vaultType)) return;

        var isVaultExists = _fileEngine.IsVaultExists(vaultType);

        var passwordWindow = new PasswordWindow(vaultType, isVaultExists);
        var password = await passwordWindow.ShowDialog<SecureString?>(this);
        if (password is null) return;

        SetLoadingState(true);

        try
        {
            if (!isVaultExists)
                await CreateNewVault(vaultType, password);

            await OpenVault(vaultType, password);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async Task OpenVault(string vaultType, SecureString password)
    {
        SetStatus("Kasa şifresi çözülüyor, lütfen bekleyin...", visible: true);
        switch (vaultType)
        {
            case "Password":
                var decryptedPasswordsJson = await _fileEngine.GetDecryptedPasswords(password);
                if (string.IsNullOrEmpty(decryptedPasswordsJson))
                {
                    SetStatus("Hatalı şifre! Lütfen tekrar deneyin.", visible: true);
                    return;
                }
        
                var passwordVaultWindow = new PasswordVaultWindow(password, decryptedPasswordsJson, _fileEngine);
                passwordVaultWindow.Show();
                Close();
                break;
            case "File":
                var decryptedFilesLookupJson = await _fileEngine.GetDecryptedFilesLookup(password);
                if (string.IsNullOrEmpty(decryptedFilesLookupJson))
                {
                    SetStatus("Hatalı şifre! Lütfen tekrar deneyin.", visible: true);
                    return;
                }
        
                var fileVaultWindow = new FileVaultWindow(password, decryptedFilesLookupJson, _fileEngine);
                fileVaultWindow.Show();
                Close();
                break;
        }
    }

    private async Task CreateNewVault(string vaultType, SecureString password)
    {
        SetStatus("Kasa oluşturuluyor, lütfen bekleyin...", visible: true);
        switch (vaultType)
        {
            case "Password":
                await _fileEngine.CreatePasswordVault(password);
                break;
            case "File":
                await _fileEngine.CreateFileVault(password);
                break;
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        MainProgressLoader.IsVisible = isLoading;
        BtnPasswordVault.IsEnabled   = !isLoading;
        BtnFileVault.IsEnabled       = !isLoading;
    }

    private void SetStatus(string message = "", bool visible = false)
    {
        TxtStatusMessage.Text      = message;
        TxtStatusMessage.IsVisible = visible;
    }
}