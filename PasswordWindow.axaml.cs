using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Vault.Models;

namespace Vault;

public partial class PasswordWindow : Window
{
    public PasswordWindow() => InitializeComponent();

    public PasswordWindow(string vaultType, bool isVaultExists) : this()
    {
        TxtPrompt.Text    = isVaultExists
            ? $"{vaultType} kasasının şifresini girin"
            : $"{vaultType} kasası için şifre belirleyin";

        BtnUnlock.Content = isVaultExists ? "Kilidi Aç" : "Oluştur";
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        TxtPassword.Focus();
    }
    
    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TrySubmit();
    }

    private void OnUnlockClick(object sender, RoutedEventArgs e) => TrySubmit();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close(null);

    private void TrySubmit()
    {
        var password = TxtPassword.Text;
        TxtPassword.Classes.Set("error", string.IsNullOrEmpty(password));
        if (string.IsNullOrEmpty(password)) return;
        
        var secure = new System.Security.SecureString();
        foreach (var c in password)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        
        TxtPassword.Text = string.Empty;

        Close(secure);
    }
}