using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace OSCode.Services;

public class ChatMessage
{
    public string Sender { get; set; } = string.Empty; // "user" or "assistant"
    public string Text { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public class SavedConversation
{
    public string SessionID { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = string.Empty;
    public string AgentMode { get; set; } = string.Empty;
    public bool YoloMode { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}

public static class ConversationHistoryManager
{
    private static readonly string ConversationsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "OSCode",
        "conversations"
    );

    private static string GetFilename(string workspacePath)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(workspacePath.Trim().ToLowerInvariant());
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        var sb = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return Path.Combine(ConversationsDir, $"{sb}.json");
    }

    public static SavedConversation? Load(string workspacePath)
    {
        try
        {
            string file = GetFilename(workspacePath);
            if (File.Exists(file))
            {
                string json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<SavedConversation>(json);
            }
        }
        catch { }
        return null;
    }

    public static void Save(string workspacePath, SavedConversation conversation)
    {
        try
        {
            if (!Directory.Exists(ConversationsDir))
            {
                Directory.CreateDirectory(ConversationsDir);
            }

            string file = GetFilename(workspacePath);
            string json = JsonSerializer.Serialize(conversation, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch { }
    }

    public static void Delete(string workspacePath)
    {
        try
        {
            string file = GetFilename(workspacePath);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch { }
    }
}
