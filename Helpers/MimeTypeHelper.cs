using System;
using System.Collections.Generic;
using System.IO;

namespace Vault;

public static class MimeTypeHelper
{
    private static readonly Dictionary<string, string> MimeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Dokümanlar
        { ".txt", "text/plain" },
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        
        // Resimler
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        
        // Videolar
        { ".mp4", "video/mp4" },
        { ".mkv", "video/x-matroska" },
        { ".avi", "video/x-msvideo" },
        { ".mov", "video/quicktime" },
        { ".wmv", "video/x-ms-wmv" },
        
        // Ses Dosyaları
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".ogg", "audio/ogg" },
        
        // Arşivler
        { ".zip", "application/zip" },
        { ".rar", "application/vnd.rar" },
        { ".7z", "application/x-7z-compressed" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" }
    };

    public static string GetMimeType(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "application/octet-stream";
        
        var extension = Path.GetExtension(fileName);

        return string.IsNullOrEmpty(extension) ? "application/octet-stream" : MimeMapping.GetValueOrDefault(extension, "application/octet-stream");
    }
}