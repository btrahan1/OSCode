using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace OSCode;

public partial class FormMain
{
    // UI Elements
    private ComboBox _cmbModel = null!;
    private ComboBox _cmbMode = null!;
    private CheckBox _chkYolo = null!;
    private Label _lblWorkspaceStatus = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnNewChat = null!;
    private TreeView _tvFiles = null!;
    private ContextMenuStrip _explorerContextMenu = null!;
    private WebView2 _wvChat = null!;
    private RichTextBox _rtbLogs = null!;
    private Label _lblStatusText = null!;

    // File Viewer Fields
    private TableLayoutPanel _pnlFileViewer = null!;
    private Label _lblViewerHeader = null!;
    private RichTextBox _rtbCodeViewer = null!;

    private void SetupDynamicLayout()
    {
        // 1. Root Layout Table (Header + Workspace split)
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

        // 3. Workspace Splitter (Left: Sidebar, Right: Main Views)
        var splitWorkspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        rootLayout.Controls.Add(splitWorkspace, 0, 1);

        // 4. Sidebar Content Container (Left Panel)
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

        _tvFiles = new TreeView
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
        pnlSidebar.Controls.Add(_tvFiles, 0, 1);

        // Context Menu Setup
        _explorerContextMenu = new ContextMenuStrip();
        var menuOpenExplorer = new ToolStripMenuItem("📂 Open in File Explorer");
        _explorerContextMenu.Items.Add(menuOpenExplorer);
        _tvFiles.ContextMenuStrip = _explorerContextMenu;

        _tvFiles.NodeMouseClick += (s, ev) =>
        {
            if (ev.Button == MouseButtons.Right)
            {
                _tvFiles.SelectedNode = ev.Node;
            }
        };

        // 5. Main Splitter (Top: Chat WebView2 + File Viewer, Bottom: Console Logs)
        var splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        splitWorkspace.Panel2.Controls.Add(splitMain);

        // Nested Vertical Split for Chat WebView2 (Left) and File Viewer (Right)
        var splitContent = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Theme.Border
        };
        splitMain.Panel1.Controls.Add(splitContent);

        // WebView2 Container (Left side of splitContent)
        _wvChat = new WebView2
        {
            Dock = DockStyle.Fill
        };
        splitContent.Panel1.Controls.Add(_wvChat);

        // File Viewer Panel (Right side of splitContent)
        _pnlFileViewer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        _pnlFileViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Title
        _pnlFileViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Close Button
        _pnlFileViewer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));       // Header row
        _pnlFileViewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));       // Code viewer row
        splitContent.Panel2.Controls.Add(_pnlFileViewer);

        _lblViewerHeader = new Label
        {
            Text = "📄 CODE VIEWER",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.HeaderFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _pnlFileViewer.Controls.Add(_lblViewerHeader, 0, 0);

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
            splitContent.SplitterDistance = splitContent.Width; // hide right panel
        };
        _pnlFileViewer.Controls.Add(btnCloseViewer, 1, 0);

        _rtbCodeViewer = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.CodeFont,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };
        _pnlFileViewer.Controls.Add(_rtbCodeViewer, 0, 1);
        _pnlFileViewer.SetColumnSpan(_rtbCodeViewer, 2);

        // 6. Logs & Output Console Panel (Bottom Panel)
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

        _rtbLogs = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain,
            Font = Theme.CodeFont,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true
        };
        pnlLogs.Controls.Add(_rtbLogs, 0, 1);

        // Wireup settings and open folder buttons click events
        _btnOpenFolder.Click += btnOpenFolder_Click;
        _btnNewChat.Click += btnNewChat_Click;
        btnSettings.Click += btnSettings_Click;
        _chkYolo.CheckedChanged += chkYolo_CheckedChanged;
        _cmbModel.SelectedIndexChanged += cmbModel_SelectedIndexChanged;
        _cmbMode.SelectedIndexChanged += cmbMode_SelectedIndexChanged;
        btnCopyContext.Click += btnCopyContext_Click;
        menuOpenExplorer.Click += btnOpenExplorer_Click;
        _tvFiles.AfterCheck += TvFiles_AfterCheck;

        // Default Split Distances
        splitWorkspace.SplitterDistance = 280;
        splitMain.SplitterDistance = 650;
        splitContent.SplitterDistance = splitContent.Width; // initially hide right code viewer
    }
}
