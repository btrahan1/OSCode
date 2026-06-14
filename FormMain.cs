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
    private OpenCodeClient _openCodeClient = null!;
    private WorkspaceController _workspaceController = null!;
    private string _workspacePath = string.Empty;
    private string _activeFilePath = string.Empty;
    private string _sessionID = string.Empty;
    private readonly Dictionary<string, string> _partTypes = new();

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

        _workspaceController = new WorkspaceController(_tvFiles);
        _workspaceController.FileSelected += WorkspaceController_FileSelected;
        _workspaceController.LogMessageReceived += msg => Log(msg);

        _openCodeClient = new OpenCodeClient(19888);
        _openCodeClient.ServerLogReceived += msg => Log($"[OpenCode Server]: {msg}");
        _openCodeClient.EventReceived += OpenCodeClient_EventReceived;

        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

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

    private async Task InitializeWebViewAsync()
    {
        await _wvChat.EnsureCoreWebView2Async(null);
        
        string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oscode-chat.html");
        if (!File.Exists(htmlPath))
        {
            File.WriteAllText(htmlPath, "<h3>Local chat template not found in output directory.</h3>");
        }

        _wvChat.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
        _wvChat.WebMessageReceived += WvChat_WebMessageReceived;
    }

    private async void WvChat_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                Log($"[User Prompt]: {text}");
                
                if (string.IsNullOrEmpty(_sessionID))
                {
                    Log("Error: No session is initialized. Please make sure a workspace is opened.");
                    return;
                }

                // Show typing indicator in UI
                _ = ExecuteJsInChatAsync("showTyping(true);");
                SetStatus("Working...", Theme.Primary);

                bool success = await _openCodeClient.SendMessageAsync(_sessionID, text, _workspacePath);
                if (!success)
                {
                    _ = ExecuteJsInChatAsync("showTyping(false);");
                    _ = ExecuteJsInChatAsync("setGenerating(false);");
                    Log("Failed to deliver message to OpenCode session.");
                    SetStatus("Ready", Theme.Success);
                }
            }
            else if (type == "cancel_prompt")
            {
                Log("Stopping current prompt...");
                if (!string.IsNullOrEmpty(_sessionID))
                {
                    await _openCodeClient.CancelSessionAsync(_sessionID);
                }

                // Re-initialize session to reset the execution queue
                string selectedModel = _cmbModel.SelectedItem?.ToString() ?? "opencode/deepseek-v4-flash-free";
                string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
                _sessionID = await _openCodeClient.CreateSessionAsync(_workspacePath, selectedModel, modeStr);
                Log($"New Session created after cancel: {_sessionID}");

                _ = ExecuteJsInChatAsync("setGenerating(false);");
                _ = ExecuteJsInChatAsync("showTyping(false);");
                SetStatus("Ready", Theme.Success);
            }
            else if (type == "permission_reply")
            {
                string requestID = root.GetProperty("requestID").GetString() ?? "";
                string reply = root.GetProperty("reply").GetString() ?? "";
                Log($"[User Permission Reply]: ID: {requestID}, Response: {reply}");
                SetStatus("Working...", Theme.Primary);
                await _openCodeClient.ReplyToPermissionAsync(requestID, reply, _workspacePath);
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
                Log($"[User Question Answers]: ID: {requestID}, Answers: {string.Join(", ", answers)}");
                SetStatus("Working...", Theme.Primary);
                await _openCodeClient.ReplyToQuestionAsync(requestID, answers, _workspacePath);
            }
        }
        catch (Exception ex)
        {
            Log($"[Error processing WebMessage]: {ex.Message}");
        }
    }

    private void OpenCodeClient_EventReceived(string eventType, string dataJson)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string, string>(OpenCodeClient_EventReceived), eventType, dataJson);
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
                            _partTypes[partId] = partType;
                        }
                    }
                    break;

                case "message.part.delta":
                    if (root.TryGetProperty("properties", out var deltaProps))
                    {
                        string partId = deltaProps.TryGetProperty("partID", out var idVal) ? idVal.GetString() ?? "" : "";
                        string delta = deltaProps.TryGetProperty("delta", out var deltaVal) ? deltaVal.GetString() ?? "" : "";

                        _partTypes.TryGetValue(partId, out string? pType);
                        pType ??= "text";

                        if (pType == "reasoning")
                        {
                            _ = ExecuteJsInChatAsync($"appendReasoningChunk({JsonSerializer.Serialize(delta)});");
                        }
                        else
                        {
                            _ = ExecuteJsInChatAsync($"appendTextChunk({JsonSerializer.Serialize(delta)});");
                        }
                    }
                    break;

                case "message.updated":
                    if (root.TryGetProperty("properties", out var msgProps) && msgProps.TryGetProperty("info", out var info))
                    {
                        if (info.TryGetProperty("finish", out var finishVal) && !string.IsNullOrEmpty(finishVal.GetString()))
                        {
                            _ = ExecuteJsInChatAsync("finalizeResponse();");
                        }
                    }
                    break;

                case "session.idle":
                    _ = ExecuteJsInChatAsync("finalizeResponse();");
                    SetStatus("Ready", Theme.Success);
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

                        Log($"[Permission Required]: ID: {reqId}, Tool: {permName}");

                        if (_chkYolo.Checked)
                        {
                            Log($"YOLO Mode is ON. Auto-approving {permName}...");
                            SetStatus("Working...", Theme.Primary);
                            _ = _openCodeClient.ReplyToPermissionAsync(reqId, "once", _workspacePath);
                        }
                        else
                        {
                            _ = ExecuteJsInChatAsync($"addPermissionWidget({JsonSerializer.Serialize(reqId)}, {JsonSerializer.Serialize(permName)}, {JsonSerializer.Serialize(details)});");
                            SetStatus("⚠️ Pending authorization", Color.FromArgb(245, 124, 0));
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

                        Log($"[Clarification Required]: ID: {reqId}, Questions Count: {questionsList.Count}");
                        string questionsJson = JsonSerializer.Serialize(questionsList);
                        _ = ExecuteJsInChatAsync($"addQuestionWidget({JsonSerializer.Serialize(reqId)}, {questionsJson});");
                        SetStatus("❓ Clarification requested", Color.FromArgb(245, 124, 0));
                    }
                    break;

                case "workspace.status":
                case "file.edited":
                    RefreshFileTreeOnMainThread();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"[Error processing Event]: {ex.Message}");
        }
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
        if (string.IsNullOrEmpty(_workspacePath))
        {
            MessageBox.Show("Please open a workspace folder first.", "Workspace Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("Starting a new conversation session...");
        
        string selectedModel = _cmbModel.SelectedItem?.ToString() ?? "opencode/deepseek-v4-flash-free";
        string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
        
        _sessionID = await _openCodeClient.CreateSessionAsync(_workspacePath, selectedModel, modeStr);
        Log($"New Session created: {_sessionID}");

        _ = ExecuteJsInChatAsync("clearChat();");
    }

    private async Task OpenWorkspaceAsync(string path)
    {
        _workspacePath = path;
        _lblWorkspaceStatus.Text = $"Active Folder: {path}";
        _lblWorkspaceStatus.ForeColor = Theme.TextMain;
        Log($"Loaded workspace: {path}");

        // Save last opened path
        var settings = SettingsManager.Load();
        settings.LastOpenedPath = path;
        SettingsManager.Save(settings);

        _workspaceController.OpenWorkspace(path);
        UpdateContextEstimation();

        // Clear UI Chat
        _ = ExecuteJsInChatAsync("clearChat();");

        // Start local OpenCode server on this folder
        await _openCodeClient.StartServerAsync(path);

        // List and populate models
        var models = await _openCodeClient.DiscoverModelsAsync();

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

        _cmbModel.Items.Clear();
        foreach (var model in sortedModels)
        {
            _cmbModel.Items.Add(model);
        }

        // Try selecting saved model
        if (!string.IsNullOrEmpty(settings.ActiveModel) && _cmbModel.Items.Contains(settings.ActiveModel))
        {
            _cmbModel.SelectedItem = settings.ActiveModel;
        }
        else if (_cmbModel.Items.Count > 0)
        {
            _cmbModel.SelectedIndex = 0;
        }

        // Try selecting saved agent mode
        if (settings.AgentMode == "plan")
        {
            _cmbMode.SelectedIndex = 1;
        }
        else
        {
            _cmbMode.SelectedIndex = 0;
        }

        // Initialize Session
        string selectedModel = _cmbModel.SelectedItem?.ToString() ?? "opencode/deepseek-v4-flash-free";
        string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
        Log($"Initializing session with model: {selectedModel} in {modeStr} mode");
        _sessionID = await _openCodeClient.CreateSessionAsync(path, selectedModel, modeStr);
        Log($"Session created: {_sessionID}");

        // Start SSE events listener
        _openCodeClient.StartEventSubscription(path);
    }

    private async void cmbModel_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_workspacePath) || _cmbModel.SelectedItem == null) return;
        string model = _cmbModel.SelectedItem.ToString()!;
        
        Log($"Changing active model to: {model}");
        var settings = SettingsManager.Load();
        settings.ActiveModel = model;
        SettingsManager.Save(settings);

        SetStatus("Reinitializing...", Theme.Primary);

        // Reset Session with new model
        string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
        _sessionID = await _openCodeClient.CreateSessionAsync(_workspacePath, model, modeStr);
        Log($"Session re-initialized: {_sessionID}");

        SetStatus("Ready", Theme.Success);
    }

    private async void cmbMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_workspacePath) || _cmbMode.SelectedItem == null) return;
        
        string modeStr = _cmbMode.SelectedIndex == 1 ? "plan" : "coder";
        Log($"Changing active mode to: {(_cmbMode.SelectedIndex == 1 ? "Plan Mode" : "Build Mode")}");
        
        var settings = SettingsManager.Load();
        settings.AgentMode = modeStr;
        SettingsManager.Save(settings);

        SetStatus("Reinitializing...", Theme.Primary);

        // Reset Session with new mode
        string selectedModel = _cmbModel.SelectedItem?.ToString() ?? "opencode/deepseek-v4-flash-free";
        _sessionID = await _openCodeClient.CreateSessionAsync(_workspacePath, selectedModel, modeStr);
        Log($"Session re-initialized in {modeStr} mode: {_sessionID}");

        SetStatus("Ready", Theme.Success);
    }

    private void chkYolo_CheckedChanged(object? sender, EventArgs e)
    {
        bool isYolo = _chkYolo.Checked;
        UpdateYoloButtonStyle(isYolo);

        var settings = SettingsManager.Load();
        settings.YoloMode = isYolo;
        SettingsManager.Save(settings);
        Log($"YOLO Mode: {(isYolo ? "ENABLED (Auto-approves all tool runs)" : "DISABLED (Requires user confirmations)")}");
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
        if (string.IsNullOrEmpty(_workspacePath))
        {
            MessageBox.Show("Please open a workspace folder first.", "Workspace Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var checkedFiles = _workspaceController.GetCheckedFiles();
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
                    string relPath = Path.GetRelativePath(_workspacePath, file);
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

    private void WorkspaceController_FileSelected(object? sender, string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            _rtbCodeViewer.Text = text;
            _activeFilePath = path;
            _lblViewerHeader.Text = $"📄 {Path.GetFileName(path)}";

            var parentSplit = _pnlFileViewer.Parent as SplitContainer;
            if (parentSplit != null && parentSplit.SplitterDistance >= parentSplit.Width - 100)
            {
                parentSplit.SplitterDistance = (int)(parentSplit.Width * 0.6); // give 60% to Chat, 40% to code viewer
            }
            Log($"Opened file in viewer: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            Log($"[Error opening file]: {ex.Message}");
        }
    }

    private void TvFiles_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        UpdateContextEstimation();
    }

    private void UpdateContextEstimation()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;

        long totalFileChars = 0;
        try
        {
            var checkedFiles = _workspaceController.GetCheckedFiles();
            foreach (var file in checkedFiles)
            {
                if (File.Exists(file))
                {
                    totalFileChars += new FileInfo(file).Length;
                }
            }
        }
        catch { }

        _ = ExecuteJsInChatAsync($"updateFileContextSize({totalFileChars});");
    }

    private void btnOpenExplorer_Click(object? sender, EventArgs e)
    {
        if (_tvFiles.SelectedNode == null || _tvFiles.SelectedNode.Tag == null) return;
        string path = _tvFiles.SelectedNode.Tag.ToString() ?? "";

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

    private void RefreshFileTreeOnMainThread()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(_workspaceController.RefreshFileTree));
        }
        else
        {
            _workspaceController.RefreshFileTree();
        }
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(Log), message);
            return;
        }

        if (string.IsNullOrEmpty(message)) return;
        _rtbLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _rtbLogs.SelectionStart = _rtbLogs.Text.Length;
        _rtbLogs.ScrollToCaret();
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string, Color>(SetStatus), text, color);
            return;
        }
        _lblStatusText.Text = text;
        _lblStatusText.ForeColor = color;
    }

    private async Task ExecuteJsInChatAsync(string jsScript)
    {
        try
        {
            if (_wvChat.CoreWebView2 != null)
            {
                await _wvChat.CoreWebView2.ExecuteScriptAsync(jsScript);
            }
        }
        catch { }
    }

    private void FormMain_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _openCodeClient?.Dispose();
    }

    private static bool IsFreeModel(string modelTag)
    {
        if (string.IsNullOrEmpty(modelTag)) return false;
        string lower = modelTag.ToLowerInvariant();
        return lower.StartsWith("ollama/") || lower.Contains("-free") || lower.Contains("/free") || lower.Contains("free");
    }
}
