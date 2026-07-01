using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Vault.Models;

public class PasswordEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("recoveryCodes")]
    public List<string> RecoveryCodes { get; set; } = [];
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}