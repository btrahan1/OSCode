using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using OSCode.Services;

namespace OSCode;

public class WorkspaceSession
{
    public string WorkspacePath { get; set; } = string.Empty;
    public string SessionID { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = "opencode/deepseek-v4-flash-free";
    public string AgentMode { get; set; } = "coder";
    public bool YoloMode { get; set; }

    public OpenCodeClient Client { get; set; } = null!;
    public WorkspaceController Controller { get; set; } = null!;
    public Dictionary<string, string> PartTypes { get; } = new();
    public List<ChatMessage> ConversationHistory { get; } = new();
    
    public string StatusText { get; set; } = "Ready";
    public Color StatusColor { get; set; } = Theme.Success;

    // UI elements specific to this tab/session
    public TreeView TvFiles { get; set; } = null!;
    public WebView2 WvChat { get; set; } = null!;
    public RichTextBox RtbCodeViewer { get; set; } = null!;
    public RichTextBox RtbLogs { get; set; } = null!;
    public SplitContainer SplitContent { get; set; } = null!;
    public TableLayoutPanel PnlFileViewer { get; set; } = null!;
    public Label LblViewerHeader { get; set; } = null!;
    
    public TabPage TabPage { get; set; } = null!;
}
