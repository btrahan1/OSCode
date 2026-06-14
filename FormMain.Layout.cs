using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace OSCode;

public partial class FormMain
{
    // Global Header & Config UI Elements
    private ComboBox _cmbModel = null!;
    private ComboBox _cmbMode = null!;
    private CheckBox _chkYolo = null!;
    private Label _lblWorkspaceStatus = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnNewChat = null!;
    private Label _lblStatusText = null!;
    private ContextMenuStrip _explorerContextMenu = null!;

    // Multiplexed Workspace Elements
    private TabControl _tabControl = null!;
    private ContextMenuStrip _tabContextMenu = null!;

    private void SetupDynamicLayout()
    {
        // 1. Root Layout Table (Header + Workspace tab control)
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Header Height
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content split
        this.Controls.Add(rootLayout);

        // 2. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = Theme.Sidebar,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0)
        };
        pnlHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Open Folder Button
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // New Chat Button
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260)); // Model dropdown (+ Settings gear)
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Agent mode dropdown
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // YOLO Mode check box
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Workspace path text
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Status Text
        rootLayout.Controls.Add(pnlHeader, 0, 0);

        _btnOpenFolder = new Button
        {
            Text = "📁 Open Folder",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.HeaderFont,
            Cursor = Cursors.Hand
        };
        _btnOpenFolder.FlatAppearance.BorderColor = Theme.Border;
        _btnOpenFolder.FlatAppearance.BorderSize = 1;
        pnlHeader.Controls.Add(_btnOpenFolder, 0, 0);

        _btnNewChat = new Button
        {
            Text = "💬 New Chat",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.HeaderFont,
            Cursor = Cursors.Hand
        };
        _btnNewChat.FlatAppearance.BorderColor = Theme.Border;
        _btnNewChat.FlatAppearance.BorderSize = 1;
        pnlHeader.Controls.Add(_btnNewChat, 1, 0);

        // Nested Panel for Model Dropdown + Settings Gear
        var pnlModelSelect = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Theme.Sidebar
        };
        pnlModelSelect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlModelSelect.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));

        _cmbModel = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.RegularFont
        };
        _cmbModel.Items.Add("opencode/deepseek-v4-flash-free");
        _cmbModel.SelectedIndex = 0;
        pnlModelSelect.Controls.Add(_cmbModel, 0, 0);

        var btnSettings = new Button
        {
            Text = "⚙",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.HeaderFont,
            Cursor = Cursors.Hand
        };
        btnSettings.FlatAppearance.BorderColor = Theme.Border;
        btnSettings.FlatAppearance.BorderSize = 1;
        pnlModelSelect.Controls.Add(btnSettings, 1, 0);
        pnlHeader.Controls.Add(pnlModelSelect, 2, 0);

        _cmbMode = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.RegularFont
        };
        _cmbMode.Items.Add("🛠 Build Mode");
        _cmbMode.Items.Add("Plan Mode");
        _cmbMode.SelectedIndex = 0;
        pnlHeader.Controls.Add(_cmbMode, 3, 0);

        _chkYolo = new CheckBox
        {
            Text = "🛡 YOLO Mode: OFF",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Appearance = Appearance.Button,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Font = Theme.HeaderFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Checked = false
        };
        _chkYolo.FlatAppearance.BorderColor = Theme.Border;
        _chkYolo.FlatAppearance.BorderSize = 1;
        pnlHeader.Controls.Add(_chkYolo, 4, 0);

        _lblWorkspaceStatus = new Label
        {
            Text = "No Workspace Folder Loaded (Anchor a folder to begin development)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TextSecondary,
            Font = Theme.HeaderFont,
            Padding = new Padding(10, 0, 0, 0)
        };
        pnlHeader.Controls.Add(_lblWorkspaceStatus, 5, 0);

        _lblStatusText = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.Success,
            Font = Theme.HeaderFont
        };
        pnlHeader.Controls.Add(_lblStatusText, 6, 0);

        // 3. TabControl for multiple concurrent workspaces
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Font = Theme.RegularFont
        };
        rootLayout.Controls.Add(_tabControl, 0, 1);

        // Context menu for TabControl (close tabs)
        _tabContextMenu = new ContextMenuStrip();
        var menuCloseTab = new ToolStripMenuItem("✕ Close Tab");
        _tabContextMenu.Items.Add(menuCloseTab);
        _tabControl.ContextMenuStrip = _tabContextMenu;

        _tabControl.MouseClick += (s, ev) =>
        {
            if (ev.Button == MouseButtons.Right)
            {
                for (int i = 0; i < _tabControl.TabCount; i++)
                {
                    if (_tabControl.GetTabRect(i).Contains(ev.Location))
                    {
                        _tabControl.SelectedIndex = i;
                        _tabContextMenu.Show(_tabControl, ev.Location);
                        break;
                    }
                }
            }
        };

        menuCloseTab.Click += btnCloseTab_Click;

        // Context Menu Setup for File Trees
        _explorerContextMenu = new ContextMenuStrip();
        var menuOpenExplorer = new ToolStripMenuItem("📂 Open in File Explorer");
        _explorerContextMenu.Items.Add(menuOpenExplorer);

        // Wireup global events
        _btnOpenFolder.Click += btnOpenFolder_Click;
        _btnNewChat.Click += btnNewChat_Click;
        btnSettings.Click += btnSettings_Click;
        _chkYolo.CheckedChanged += chkYolo_CheckedChanged;
        _cmbModel.SelectedIndexChanged += cmbModel_SelectedIndexChanged;
        _cmbMode.SelectedIndexChanged += cmbMode_SelectedIndexChanged;
        menuOpenExplorer.Click += btnOpenExplorer_Click;
    }

    public WorkspaceSession CreateWorkspaceTab(string path)
    {
        var session = new WorkspaceSession
        {
            WorkspacePath = path,
            ActiveModel = _cmbModel.SelectedItem?.ToString() ?? "opencode/deepseek-v4-flash-free",
            AgentMode = _cmbMode.SelectedIndex == 1 ? "plan" : "coder",
            YoloMode = _chkYolo.Checked
        };

        var tabPage = new TabPage
        {
            Text = $"📁 {Path.GetFileName(path)}",
            BackColor = Theme.Background,
            Tag = session
        };
        session.TabPage = tabPage;

        // Workspace Splitter (Left: Sidebar, Right: Main Views)
        var splitWorkspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        tabPage.Controls.Add(splitWorkspace);

        // Sidebar Content Container (Left Panel)
        var pnlSidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Sidebar,
            Padding = new Padding(6),
            Margin = new Padding(0)
        };
        pnlSidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Label Header
        pnlSidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tree View
        splitWorkspace.Panel1.Controls.Add(pnlSidebar);

        var pnlSidebarHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        pnlSidebarHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Header Label
        pnlSidebarHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Copy Button

        var lblFilesHeader = new Label
        {
            Text = "📂 WORKSPACE EXPLORER",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextMain,
            Font = Theme.HeaderFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlSidebarHeader.Controls.Add(lblFilesHeader, 0, 0);

        var btnCopyContext = new Button
        {
            Text = "📋 Copy Context",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Font = new Font(Theme.RegularFont.FontFamily, 7.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCopyContext.FlatAppearance.BorderColor = Theme.Border;
        btnCopyContext.FlatAppearance.BorderSize = 1;
        pnlSidebarHeader.Controls.Add(btnCopyContext, 1, 0);
        pnlSidebar.Controls.Add(pnlSidebarHeader, 0, 0);

        session.TvFiles = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            LineColor = Theme.Border,
            BorderStyle = BorderStyle.None,
            Font = Theme.RegularFont,
            Indent = 15,
            CheckBoxes = true
        };
        pnlSidebar.Controls.Add(session.TvFiles, 0, 1);
        session.TvFiles.ContextMenuStrip = _explorerContextMenu;

        session.TvFiles.NodeMouseClick += (s, ev) =>
        {
            if (ev.Button == MouseButtons.Right)
            {
                session.TvFiles.SelectedNode = ev.Node;
            }
        };

        // Main Splitter (Top: Chat WebView2 + File Viewer, Bottom: Console Logs)
        var splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        splitWorkspace.Panel2.Controls.Add(splitMain);

        // Nested Vertical Split for Chat WebView2 (Left) and File Viewer (Right)
        session.SplitContent = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        splitMain.Panel1.Controls.Add(session.SplitContent);

        // WebView2 Container (Left side of splitContent)
        session.WvChat = new WebView2
        {
            Dock = DockStyle.Fill
        };
        session.SplitContent.Panel1.Controls.Add(session.WvChat);

        // File Viewer Panel (Right side of splitContent)
        session.PnlFileViewer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        session.PnlFileViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Title
        session.PnlFileViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Close Button
        session.PnlFileViewer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));       // Header row
        session.PnlFileViewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));       // Code viewer row
        session.SplitContent.Panel2.Controls.Add(session.PnlFileViewer);

        session.LblViewerHeader = new Label
        {
            Text = "📄 CODE VIEWER",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.HeaderFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        session.PnlFileViewer.Controls.Add(session.LblViewerHeader, 0, 0);

        var btnCloseViewer = new Button
        {
            Text = "Close ✕",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Font = Theme.RegularFont,
            Cursor = Cursors.Hand
        };
        btnCloseViewer.FlatAppearance.BorderColor = Theme.Border;
        btnCloseViewer.FlatAppearance.BorderSize = 1;
        btnCloseViewer.Click += (s, ev) => {
            session.SplitContent.SplitterDistance = session.SplitContent.Width; // hide right panel
        };
        session.PnlFileViewer.Controls.Add(btnCloseViewer, 1, 0);

        session.RtbCodeViewer = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.CodeFont,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };
        session.PnlFileViewer.Controls.Add(session.RtbCodeViewer, 0, 1);
        session.PnlFileViewer.SetColumnSpan(session.RtbCodeViewer, 2);

        // Logs & Output Console Panel (Bottom Panel)
        var pnlLogs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(6),
            Margin = new Padding(0)
        };
        pnlLogs.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Logs Title
        pnlLogs.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Text Box
        splitMain.Panel2.Controls.Add(pnlLogs);

        var lblLogsHeader = new Label
        {
            Text = "🖥 LOCAL OPENCODE EXECUTION LOGS",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.HeaderFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlLogs.Controls.Add(lblLogsHeader, 0, 0);

        session.RtbLogs = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.CodeFont,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true
        };
        pnlLogs.Controls.Add(session.RtbLogs, 0, 1);

        // Wireup session-specific events
        btnCopyContext.Click += btnCopyContext_Click;
        session.TvFiles.AfterCheck += TvFiles_AfterCheck;

        // Default Split Distances
        splitWorkspace.SplitterDistance = 280;
        splitMain.SplitterDistance = 650;
        session.SplitContent.SplitterDistance = session.SplitContent.Width; // initially hide right code viewer

        _tabControl.TabPages.Add(tabPage);
        _tabControl.SelectedTab = tabPage;

        return session;
    }
}
