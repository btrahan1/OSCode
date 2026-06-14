using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OSCode.Services;
using System.Collections.Generic;

namespace OSCode;

public partial class FormMain : Form
{
    private string _activeFilePath = string.Empty;
    private WorkspaceSession? ActiveSession => _tabControl.SelectedTab?.Tag as WorkspaceSession;

    public FormMain()
    {
        // Set standard window preferences
        this.Text = "OSCode - Local Agent Workspace";
        this.Size = new Size(1500, 950);
        this.MinimumSize = new Size(1000, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Theme.Background;
        this.Font = Theme.RegularFont;

        SetupDynamicLayout();
        this.Load += FormMain_Load;
        this.FormClosed += FormMain_FormClosed;
    }

    private async void FormMain_Load(object? sender, EventArgs e)
    {
        Log("Initializing OSCode workspace...");

        _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

        var settings = SettingsManager.Load();
        _chkYolo.Checked = settings.YoloMode;
        UpdateYoloButtonStyle(settings.YoloMode);

        if (!string.IsNullOrEmpty(settings.LastOpenedPath) && Directory.Exists(settings.LastOpenedPath))
        {
            await OpenWorkspaceAsync(settings.LastOpenedPath);
        }
        else
        {
            Log("Ready. Open a folder to target development.");
        }
    }

    private async Task InitializeSessionWebViewAsync(WorkspaceSession session)
    {
        await session.WvChat.EnsureCoreWebView2Async(null);
        
        string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oscode-chat.html");
        if (!File.Exists(htmlPath))
        {
            File.WriteAllText(htmlPath, "<h3>Local chat template not found in output directory.</h3>");
        }

        session.WvChat.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
        session.WvChat.WebMessageReceived += (s, e) => WvChat_SessionWebMessageReceived(session, e);
    }

    private async void WvChat_SessionWebMessageReceived(WorkspaceSession session, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string rawJson = e.WebMessageAsJson;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;

            string type = typeProp.GetString() ?? "";
            if (type == "user_prompt")
            {
                string text = root.GetProperty("text").GetString() ?? "";
                Log($"[User Prompt]: {text}", session);
                
                if (string.IsNullOrEmpty(session.SessionID))
                {
                    Log("Error: No session is initialized. Please make sure a workspace is opened.", session);
                    return;
                }

                // Show typing indicator in UI
                _ = ExecuteJsInChatAsync(session, "showTyping(true);");
                SetStatus(session, "Working...", Theme.Primary);

                bool success = await session.Client.SendMessageAsync(session.SessionID, text, session.WorkspacePath);
                if (!success)
                {
                    _ = ExecuteJsInChatAsync(session, "showTyping(false);");
                    _ = ExecuteJsInChatAsync(session, "setGenerating(false);");
                    Log("Failed to deliver message to OpenCode session.", session);
                    SetStatus(session, "Ready", Theme.Success);
                }
            }
            else if (type == "cancel_prompt")
            {
                Log("Stopping current prompt...", session);
                if (!string.IsNullOrEmpty(session.SessionID))
                {
                    await session.Client.CancelSessionAsync(session.SessionID);
                }

                // Re-initialize session to reset the execution queue
                string selectedModel = session.ActiveModel;
                string modeStr = session.AgentMode;
                session.SessionID = await session.Client.CreateSessionAsync(session.WorkspacePath, selectedModel, modeStr);
                Log($"New Session created after cancel: {session.SessionID}", session);

                _ = ExecuteJsInChatAsync(session, "setGenerating(false);");
                _ = ExecuteJsInChatAsync(session, "showTyping(false);");
                SetStatus(session, "Ready", Theme.Success);
            }
            else if (type == "permission_reply")
            {
                string requestID = root.GetProperty("requestID").GetString() ?? "";
                string reply = root.GetProperty("reply").GetString() ?? "";
                Log($"[User Permission Reply]: ID: {requestID}, Response: {reply}", session);
                SetStatus(session, "Working...", Theme.Primary);
                await session.Client.ReplyToPermissionAsync(requestID, reply, session.WorkspacePath);
            }
            else if (type == "question_reply")
            {
                string requestID = root.GetProperty("requestID").GetString() ?? "";
                var answers = new List<string>();
                if (root.TryGetProperty("answers", out var answersProp) && answersProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in answersProp.EnumerateArray())
                    {
                        answers.Add(elem.GetString() ?? "");
                    }
                }
                Log($"[User Question Answers]: ID: {requestID}, Answers: {string.Join(", ", answers)}", session);
                SetStatus(session, "Working...", Theme.Primary);
                await session.Client.ReplyToQuestionAsync(requestID, answers, session.WorkspacePath);
            }
        }
        catch (Exception ex)
        {
            Log($"[Error processing WebMessage]: {ex.Message}");
        }
    }

    private void OpenCodeClient_SessionEventReceived(WorkspaceSession session, string eventType, string dataJson)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<WorkspaceSession, string, string>(OpenCodeClient_SessionEventReceived), session, eventType, dataJson);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            var root = doc.RootElement;

            string actualEventType = eventType;
            if (root.TryGetProperty("type", out var typeProp))
            {
                actualEventType = typeProp.GetString() ?? eventType;
            }

            switch (actualEventType)
            {
                case "message.part.updated":
                    if (root.TryGetProperty("properties", out var props) && props.TryGetProperty("part", out var part))
                    {
                        string partId = part.TryGetProperty("id", out var idVal) ? idVal.GetString() ?? "" : "";
                        string partType = part.TryGetProperty("type", out var typeVal) ? typeVal.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(partId))
                        {
                            session.PartTypes[partId] = partType;
                        }
                    }
                    break;

                case "message.part.delta":
                    if (root.TryGetProperty("properties", out var deltaProps))
                    {
                        string partId = deltaProps.TryGetProperty("partID", out var idVal) ? idVal.GetString() ?? "" : "";
                        string delta = deltaProps.TryGetProperty("delta", out var deltaVal) ? deltaVal.GetString() ?? "" : "";

                        session.PartTypes.TryGetValue(partId, out string? pType);
                        pType ??= "text";

                        if (pType == "reasoning")
                        {
                            _ = ExecuteJsInChatAsync(session, $"appendReasoningChunk({JsonSerializer.Serialize(delta)});");
                        }
                        else
                        {
                            _ = ExecuteJsInChatAsync(session, $"appendTextChunk({JsonSerializer.Serialize(delta)});");
                        }
                    }
                    break;

                case "message.updated":
                    if (root.TryGetProperty("properties", out var msgProps) && msgProps.TryGetProperty("info", out var info))
                    {
                        if (info.TryGetProperty("finish", out var finishVal) && !string.IsNullOrEmpty(finishVal.GetString()))
                        {
                            _ = ExecuteJsInChatAsync(session, "finalizeResponse();");
                        }
                    }
                    break;

                case "session.idle":
                    _ = ExecuteJsInChatAsync(session, "finalizeResponse();");
                    SetStatus(session, "Ready", Theme.Success);
                    break;

                case "permission.asked":
                    if (root.TryGetProperty("properties", out var permProps))
                    {
                        string reqId = permProps.TryGetProperty("id", out var idVal) ? idVal.GetString() ?? "" : "";
                        string permName = permProps.TryGetProperty("permission", out var permVal) ? permVal.GetString() ?? "" : "";
                        string details = "";

                        if (permProps.TryGetProperty("metadata", out var meta))
                        {
                            details = meta.ToString() ?? "";
                        }

                        Log($"[Permission Required]: ID: {reqId}, Tool: {permName}", session);

                        if (session.YoloMode)
                        {
                            Log($"YOLO Mode is ON. Auto-approving {permName}...", session);
                            SetStatus(session, "Working...", Theme.Primary);
                            _ = session.Client.ReplyToPermissionAsync(reqId, "once", session.WorkspacePath);
                        }
                        else
                        {
                            _ = ExecuteJsInChatAsync(session, $"addPermissionWidget({JsonSerializer.Serialize(reqId)}, {JsonSerializer.Serialize(permName)}, {JsonSerializer.Serialize(details)});");
                            SetStatus(session, "⚠️ Pending authorization", Color.FromArgb(245, 124, 0));
                        }
                    }
                    break;

                case "question.asked":
                    if (root.TryGetProperty("properties", out var qProps))
                    {
                        string reqId = qProps.TryGetProperty("id", out var idVal) ? idVal.GetString() ?? "" : "";
                        
                        var questionsList = new List<string>();
                        if (qProps.TryGetProperty("questions", out var questionsArr) && questionsArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var qObj in questionsArr.EnumerateArray())
                            {
                                string text = qObj.TryGetProperty("question", out var qVal) ? qVal.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(text))
                                {
                                    questionsList.Add(text);
                                }
                            }
                        }

                        if (questionsList.Count == 0 && qProps.TryGetProperty("question", out var singleQ))
                        {
                            string text = singleQ.GetString() ?? "";
                            if (!string.IsNullOrEmpty(text))
                            {
                                questionsList.Add(text);
                            }
                        }

                        Log($"[Clarification Required]: ID: {reqId}, Questions Count: {questionsList.Count}", session);
                        string questionsJson = JsonSerializer.Serialize(questionsList);
                        _ = ExecuteJsInChatAsync(session, $"addQuestionWidget({JsonSerializer.Serialize(reqId)}, {questionsJson});");
                        SetStatus(session, "❓ Clarification requested", Color.FromArgb(245, 124, 0));
                    }
                    break;

                case "workspace.status":
                case "file.edited":
                    RefreshFileTreeOnMainThread(session);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"[Error processing Event]: {ex.Message}");
        }
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null)
        {
            _lblWorkspaceStatus.Text = "No Workspace Folder Loaded (Anchor a folder to begin development)";
            _lblWorkspaceStatus.ForeColor = Theme.TextSecondary;
            _lblStatusText.Text = "Ready";
            _lblStatusText.ForeColor = Theme.Success;
            return;
        }

        _lblWorkspaceStatus.Text = $"Active Folder: {session.WorkspacePath}";
        _lblWorkspaceStatus.ForeColor = Theme.TextMain;

        _lblStatusText.Text = session.StatusText;
        _lblStatusText.ForeColor = session.StatusColor;

        // Prevent firing change events while syncing states
        _chkYolo.Checked = session.YoloMode;
        UpdateYoloButtonStyle(session.YoloMode);

        if (!_cmbModel.Items.Contains(session.ActiveModel))
        {
            _cmbModel.Items.Add(session.ActiveModel);
        }
        _cmbModel.SelectedItem = session.ActiveModel;

        _cmbMode.SelectedIndex = session.AgentMode == "plan" ? 1 : 0;
    }

    private void btnOpenFolder_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        fbd.Description = "Select Workspace Development Folder";
        fbd.UseDescriptionForTitle = true;

        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _ = OpenWorkspaceAsync(fbd.SelectedPath);
        }
    }

    private async void btnNewChat_Click(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null)
        {
            MessageBox.Show("Please open a workspace folder first.", "Workspace Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("Starting a new conversation session...", session);
        
        session.SessionID = await session.Client.CreateSessionAsync(session.WorkspacePath, session.ActiveModel, session.AgentMode);
        Log($"New Session created: {session.SessionID}", session);

        _ = ExecuteJsInChatAsync(session, "clearChat();");
    }

    private async Task OpenWorkspaceAsync(string path)
    {
        Log($"Loading workspace: {path}");

        // Create the WorkspaceSession and visual TabPage layout
        var session = CreateWorkspaceTab(path);

        session.Controller = new WorkspaceController(session.TvFiles);
        session.Controller.FileSelected += (s, filePath) => WorkspaceController_FileSelected(session, filePath);
        session.Controller.LogMessageReceived += msg => Log(msg, session);

        session.Client = new OpenCodeClient(19888);
        session.Client.ServerLogReceived += msg => Log($"[OpenCode Server]: {msg}", session);
        session.Client.EventReceived += (evt, data) => OpenCodeClient_SessionEventReceived(session, evt, data);

        // Save last opened path
        var settings = SettingsManager.Load();
        settings.LastOpenedPath = path;
        SettingsManager.Save(settings);

        session.Controller.OpenWorkspace(path);
        UpdateContextEstimation(session);

        // Initialize WebView
        try
        {
            await InitializeSessionWebViewAsync(session);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Start local OpenCode server on this folder
        await session.Client.StartServerAsync(path);

        // List and populate models
        var models = await session.Client.DiscoverModelsAsync();

        // Sort models: free/local first, then paid
        var sortedModels = new List<string>();
        foreach (var m in models)
        {
            if (IsFreeModel(m)) sortedModels.Add(m);
        }
        foreach (var m in models)
        {
            if (!IsFreeModel(m)) sortedModels.Add(m);
        }

        foreach (var model in sortedModels)
        {
            if (!_cmbModel.Items.Contains(model))
            {
                _cmbModel.Items.Add(model);
            }
        }

        // Try selecting saved model or the first discovered one
        if (_cmbModel.Items.Contains(settings.ActiveModel))
        {
            session.ActiveModel = settings.ActiveModel;
        }
        else if (sortedModels.Count > 0)
        {
            session.ActiveModel = sortedModels[0];
        }

        // Sync header UI controls for this session
        TabControl_SelectedIndexChanged(null, EventArgs.Empty);

        // Initialize Session
        Log($"Initializing session with model: {session.ActiveModel} in {session.AgentMode} mode", session);
        session.SessionID = await session.Client.CreateSessionAsync(path, session.ActiveModel, session.AgentMode);
        Log($"Session created: {session.SessionID}", session);

        // Start SSE events listener
        session.Client.StartEventSubscription(path);
    }

    private async void cmbModel_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null || _cmbModel.SelectedItem == null) return;
        string model = _cmbModel.SelectedItem.ToString()!;
        
        // Prevent trigger during tab selection sync
        if (session.ActiveModel == model) return;

        Log($"Changing active model to: {model}", session);
        session.ActiveModel = model;

        var settings = SettingsManager.Load();
        settings.ActiveModel = model;
        SettingsManager.Save(settings);

        SetStatus(session, "Reinitializing...", Theme.Primary);

        // Reset Session with new model
        session.SessionID = await session.Client.CreateSessionAsync(session.WorkspacePath, model, session.AgentMode);
        Log($"Session re-initialized: {session.SessionID}", session);

        SetStatus(session, "Ready", Theme.Success);
    }

    private async void cmbMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null || _cmbMode.SelectedItem == null) return;
        
        string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
        
        // Prevent trigger during tab selection sync
        if (session.AgentMode == modeStr) return;

        Log($"Changing active mode to: {(modeStr == "plan" ? "Plan Mode" : "Build Mode")}", session);
        session.AgentMode = modeStr;
        
        var settings = SettingsManager.Load();
        settings.AgentMode = modeStr;
        SettingsManager.Save(settings);

        SetStatus(session, "Reinitializing...", Theme.Primary);

        // Reset Session with new mode
        session.SessionID = await session.Client.CreateSessionAsync(session.WorkspacePath, session.ActiveModel, modeStr);
        Log($"Session re-initialized in {modeStr} mode: {session.SessionID}", session);

        SetStatus(session, "Ready", Theme.Success);
    }

    private void chkYolo_CheckedChanged(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null) return;

        bool isYolo = _chkYolo.Checked;
        if (session.YoloMode == isYolo) return;

        session.YoloMode = isYolo;
        UpdateYoloButtonStyle(isYolo);

        var settings = SettingsManager.Load();
        settings.YoloMode = isYolo;
        SettingsManager.Save(settings);
        Log($"YOLO Mode: {(isYolo ? "ENABLED (Auto-approves all tool runs)" : "DISABLED (Requires user confirmations)")}", session);
    }

    private void UpdateYoloButtonStyle(bool isYolo)
    {
        if (isYolo)
        {
            _chkYolo.Text = "⚡ YOLO Mode: ON";
            _chkYolo.BackColor = Theme.Success;
            _chkYolo.ForeColor = Color.White;
        }
        else
        {
            _chkYolo.Text = "🛡 YOLO Mode: OFF";
            _chkYolo.BackColor = Theme.Surface;
            _chkYolo.ForeColor = Theme.TextSecondary;
        }
    }

    private void btnSettings_Click(object? sender, EventArgs e)
    {
        var settings = SettingsManager.Load();
        using var dlg = new FormSettings(settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SettingsManager.Save(settings);
            Log("Settings saved successfully.");
        }
    }

    private void btnCopyContext_Click(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null)
        {
            MessageBox.Show("Please open a workspace folder first.", "Workspace Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var checkedFiles = session.Controller.GetCheckedFiles();
        if (checkedFiles.Count == 0)
        {
            MessageBox.Show("Please select one or more files in the workspace explorer first.", "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var sb = new StringBuilder();
            foreach (var file in checkedFiles)
            {
                if (File.Exists(file))
                {
                    string content = File.ReadAllText(file);
                    string relPath = Path.GetRelativePath(session.WorkspacePath, file);
                    sb.AppendLine($"// FILE: {relPath}");
                    sb.AppendLine("```");
                    sb.AppendLine(content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"Successfully copied context for {checkedFiles.Count} files to clipboard!", "Context Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying files to clipboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void WorkspaceController_FileSelected(WorkspaceSession session, string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            session.RtbCodeViewer.Text = text;
            _activeFilePath = path;
            session.LblViewerHeader.Text = $"📄 {Path.GetFileName(path)}";

            if (session.SplitContent.SplitterDistance >= session.SplitContent.Width - 100)
            {
                session.SplitContent.SplitterDistance = (int)(session.SplitContent.Width * 0.6); // give 60% to Chat, 40% to code viewer
            }
            Log($"Opened file in viewer: {Path.GetFileName(path)}", session);
        }
        catch (Exception ex)
        {
            Log($"[Error opening file]: {ex.Message}");
        }
    }

    private void TvFiles_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab.Tag is WorkspaceSession session && session.TvFiles == sender)
            {
                UpdateContextEstimation(session);
                break;
            }
        }
    }

    private void UpdateContextEstimation(WorkspaceSession session)
    {
        long totalFileChars = 0;
        try
        {
            var checkedFiles = session.Controller.GetCheckedFiles();
            foreach (var file in checkedFiles)
            {
                if (File.Exists(file))
                {
                    totalFileChars += new FileInfo(file).Length;
                }
            }
        }
        catch { }

        _ = ExecuteJsInChatAsync(session, $"updateFileContextSize({totalFileChars});");
    }

    private void btnOpenExplorer_Click(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null || session.TvFiles.SelectedNode == null || session.TvFiles.SelectedNode.Tag == null) return;
        string path = session.TvFiles.SelectedNode.Tag.ToString() ?? "";

        try
        {
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Explorer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnCloseTab_Click(object? sender, EventArgs e)
    {
        var session = ActiveSession;
        if (session == null) return;

        var result = MessageBox.Show($"Are you sure you want to close the workspace '{Path.GetFileName(session.WorkspacePath)}'?", "Close Workspace", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            session.Client?.Dispose();
            session.WvChat?.Dispose();

            _tabControl.TabPages.Remove(session.TabPage);

            TabControl_SelectedIndexChanged(null, EventArgs.Empty);
        }
    }

    private void RefreshFileTreeOnMainThread(WorkspaceSession session)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(session.Controller.RefreshFileTree));
        }
        else
        {
            session.Controller.RefreshFileTree();
        }
    }

    private void Log(string message, WorkspaceSession? session = null)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string, WorkspaceSession?>(Log), message, session);
            return;
        }

        if (string.IsNullOrEmpty(message)) return;

        var targetSession = session ?? ActiveSession;
        if (targetSession != null && targetSession.RtbLogs != null)
        {
            var rtb = targetSession.RtbLogs;
            rtb.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            rtb.SelectionStart = rtb.Text.Length;
            rtb.ScrollToCaret();
        }
        else
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }

    private void SetStatus(WorkspaceSession session, string text, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<WorkspaceSession, string, Color>(SetStatus), session, text, color);
            return;
        }
        session.StatusText = text;
        session.StatusColor = color;

        if (session == ActiveSession)
        {
            _lblStatusText.Text = text;
            _lblStatusText.ForeColor = color;
        }
    }

    private async Task ExecuteJsInChatAsync(WorkspaceSession session, string jsScript)
    {
        try
        {
            if (session.WvChat.CoreWebView2 != null)
            {
                await session.WvChat.CoreWebView2.ExecuteScriptAsync(jsScript);
            }
        }
        catch { }
    }

    private void FormMain_FormClosed(object? sender, FormClosedEventArgs e)
    {
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab.Tag is WorkspaceSession session)
            {
                session.Client?.Dispose();
            }
        }
    }

    private static bool IsFreeModel(string modelTag)
    {
        if (string.IsNullOrEmpty(modelTag)) return false;
        string lower = modelTag.ToLowerInvariant();
        return lower.StartsWith("ollama/") || lower.Contains("-free") || lower.Contains("/free") || lower.Contains("free");
    }
}
