using System;
using System.IO;
using System.Text.Json;

namespace OSCode;

public class AppSettings
{
    public string LastOpenedPath { get; set; } = string.Empty;
    public string SqlConnection { get; set; } = @"Server=MSI\SQLEXPRESS01;Database=master;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;";
    public string ActiveModel { get; set; } = "opencode/deepseek-v4-flash-free";
    public bool YoloMode { get; set; } = false;
    public string AgentMode { get; set; } = "coder";
}

public static class SettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "OSCode"
    );
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
