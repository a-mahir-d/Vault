using System;
using System.Text.Json.Serialization;

namespace Vault.Models;

public class FileLookupEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("originalName")]
    public string OriginalName { get; set; } = string.Empty;
    
    [JsonPropertyName("fileSize")]
    public ulong FileSize { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;
    
    [JsonPropertyName("encryptionKey")]
    public string EncryptionKey { get; set; } = string.Empty;
    
    [JsonPropertyName("iv")]
    public string Iv { get; set; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}