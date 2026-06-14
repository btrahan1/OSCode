using System;
using System.Drawing;
using System.Windows.Forms;

namespace OSCode;

public class FormSettings : Form
{
    private readonly TextBox _txtSqlConnection;
    private readonly AppSettings _settings;

    public FormSettings(AppSettings settings)
    {
        _settings = settings;

        this.Width = 500;
        this.Height = 180;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.Text = "Configure Settings";
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Theme.Sidebar;
        this.ForeColor = Theme.TextMain;
        this.Font = Theme.RegularFont;

        var lblSql = new Label() 
        { 
            Left = 20, 
            Top = 20, 
            Text = "SQL Connection String:", 
            Width = 460, 
            Font = Theme.HeaderFont 
        };

        _txtSqlConnection = new TextBox() 
        { 
            Left = 20, 
            Top = 45, 
            Width = 440, 
            Text = settings.SqlConnection, 
            Font = Theme.RegularFont, 
            BackColor = Theme.Surface, 
            ForeColor = Theme.TextMain, 
            BorderStyle = BorderStyle.FixedSingle 
        };

        var btnSave = new Button() 
        { 
            Text = "Save", 
            Left = 350, 
            Width = 110, 
            Top = 95, 
            DialogResult = DialogResult.OK, 
            FlatStyle = FlatStyle.Flat, 
            BackColor = Theme.Surface, 
            ForeColor = Theme.TextMain 
        };
        btnSave.FlatAppearance.BorderColor = Theme.Border;

        var btnCancel = new Button() 
        { 
            Text = "Cancel", 
            Left = 230, 
            Width = 110, 
            Top = 95, 
            DialogResult = DialogResult.Cancel, 
            FlatStyle = FlatStyle.Flat, 
            BackColor = Theme.Surface, 
            ForeColor = Theme.TextMain 
        };
        btnCancel.FlatAppearance.BorderColor = Theme.Border;

        this.Controls.Add(lblSql);
        this.Controls.Add(_txtSqlConnection);
        this.Controls.Add(btnSave);
        this.Controls.Add(btnCancel);

        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (this.DialogResult == DialogResult.OK)
        {
            _settings.SqlConnection = _txtSqlConnection.Text;
        }
    }
}
