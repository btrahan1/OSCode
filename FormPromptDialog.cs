using System;
using System.Drawing;
using System.Windows.Forms;

namespace OSCode;

public class FormPromptDialog : Form
{
    private readonly TextBox _txtValue;

    public string InputText => _txtValue.Text;

    public FormPromptDialog(string promptText, string title, string defaultVal = "")
    {
        this.Width = 400;
        this.Height = 160;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.Text = title;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Theme.Background;
        this.ForeColor = Theme.TextMain;
        this.Font = Theme.RegularFont;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var lblText = new Label() 
        { 
            Left = 20, 
            Top = 15, 
            Text = promptText, 
            AutoSize = true 
        };

        _txtValue = new TextBox() 
        { 
            Left = 20, 
            Top = 40, 
            Width = 340, 
            Text = defaultVal, 
            BorderStyle = BorderStyle.FixedSingle, 
            BackColor = Theme.Surface, 
            ForeColor = Theme.TextMain 
        };

        var btnOk = new Button() 
        { 
            Text = "Ok", 
            Left = 270, 
            Width = 90, 
            Top = 80, 
            DialogResult = DialogResult.OK, 
            FlatStyle = FlatStyle.Flat, 
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain
        };
        btnOk.FlatAppearance.BorderColor = Theme.Border;

        var btnCancel = new Button() 
        { 
            Text = "Cancel", 
            Left = 170, 
            Width = 90, 
            Top = 80, 
            DialogResult = DialogResult.Cancel, 
            FlatStyle = FlatStyle.Flat, 
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMain
        };
        btnCancel.FlatAppearance.BorderColor = Theme.Border;

        this.Controls.Add(lblText);
        this.Controls.Add(_txtValue);
        this.Controls.Add(btnOk);
        this.Controls.Add(btnCancel);
        
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;
    }

    public static string Show(string promptText, string title, string defaultVal = "")
    {
        using var dlg = new FormPromptDialog(promptText, title, defaultVal);
        return dlg.ShowDialog() == DialogResult.OK ? dlg.InputText : "";
    }
}
