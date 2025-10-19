using System;
using System.Windows.Forms;

namespace NCEF
{
    public class PromptForm : Form
    {
        private TextBox textBox;
        private Button btnOk;
        private Button btnCancel;
        private Label label;

        public string InputText => textBox.Text;

        public PromptForm(string message, string defaultText = "")
        {
            this.Text = "Prompt";
            this.Width = 400;
            this.Height = 150;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            label = new Label() { Left = 10, Top = 10, Text = message, Width = 360 };
            textBox = new TextBox() { Left = 10, Top = 40, Width = 360, Text = defaultText };

            btnOk = new Button() { Text = "OK", Left = 210, Width = 75, Top = 70, DialogResult = DialogResult.OK };
            btnCancel = new Button() { Text = "Cancel", Left = 295, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}