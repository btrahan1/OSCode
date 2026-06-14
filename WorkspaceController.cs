using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace OSCode;

public class WorkspaceController
{
    private readonly TreeView _tvFiles;
    private string _workspacePath = string.Empty;
    private bool _isUpdatingCheck = false;

    public string WorkspacePath => _workspacePath;

    public event EventHandler<string>? FileSelected;
    public event Action<string>? LogMessageReceived;

    public WorkspaceController(TreeView tvFiles)
    {
        _tvFiles = tvFiles;
        _tvFiles.NodeMouseDoubleClick += TvFiles_NodeMouseDoubleClick;
        _tvFiles.AfterCheck += TvFiles_AfterCheck;
    }

    public void OpenWorkspace(string path)
    {
        _workspacePath = path;
        RefreshFileTree();
    }

    public void RefreshFileTree()
    {
        _tvFiles.Nodes.Clear();
        if (string.IsNullOrEmpty(_workspacePath) || !Directory.Exists(_workspacePath)) return;

        try
        {
            var rootDirectoryInfo = new DirectoryInfo(_workspacePath);
            var rootNode = CreateDirectoryNode(rootDirectoryInfo);
            _tvFiles.Nodes.Add(rootNode);
            rootNode.Expand();
        }
        catch (Exception ex)
        {
            LogMessageReceived?.Invoke($"[Error building file tree]: {ex.Message}");
        }
    }

    private TreeNode CreateDirectoryNode(DirectoryInfo directoryInfo)
    {
        var directoryNode = new TreeNode(directoryInfo.Name)
        {
            Tag = directoryInfo.FullName,
            ForeColor = Theme.TextMain
        };

        try
        {
            foreach (var directory in directoryInfo.GetDirectories())
            {
                if (directory.Name == "bin" || directory.Name == "obj" || directory.Name == ".git" || directory.Name == "node_modules")
                    continue;
                directoryNode.Nodes.Add(CreateDirectoryNode(directory));
            }
            foreach (var file in directoryInfo.GetFiles())
            {
                var fileNode = new TreeNode(file.Name)
                {
                    Tag = file.FullName,
                    ForeColor = Theme.TextSecondary
                };
                directoryNode.Nodes.Add(fileNode);
            }
        }
        catch { }

        return directoryNode;
    }

    private void TvFiles_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node == null || e.Node.Tag == null) return;
        string path = e.Node.Tag.ToString() ?? "";

        if (File.Exists(path))
        {
            FileSelected?.Invoke(this, path);
        }
    }

    private void TvFiles_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (e.Node == null || _isUpdatingCheck) return;
        _isUpdatingCheck = true;
        try
        {
            UpdateChildChecks(e.Node, e.Node.Checked);
        }
        finally
        {
            _isUpdatingCheck = false;
        }
    }

    private void UpdateChildChecks(TreeNode node, bool isChecked)
    {
        foreach (TreeNode child in node.Nodes)
        {
            child.Checked = isChecked;
            UpdateChildChecks(child, isChecked);
        }
    }

    public List<string> GetCheckedFiles()
    {
        var checkedFiles = new List<string>();
        foreach (TreeNode node in _tvFiles.Nodes)
        {
            CollectCheckedFiles(node, checkedFiles);
        }
        return checkedFiles;
    }

    private void CollectCheckedFiles(TreeNode node, List<string> files)
    {
        string path = node.Tag?.ToString() ?? "";
        if (node.Checked && File.Exists(path))
        {
            files.Add(path);
        }
        foreach (TreeNode child in node.Nodes)
        {
            CollectCheckedFiles(child, files);
        }
    }
}
