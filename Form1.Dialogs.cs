using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private sealed class RegistrationDialog : Form
    {
        private readonly TextBox _usernameTextBox = new();
        private readonly TextBox _emailTextBox = new();
        private readonly TextBox _passwordTextBox = new();
        private readonly TextBox _confirmPasswordTextBox = new();

        public string Username => _usernameTextBox.Text.Trim();
        public string Email => _emailTextBox.Text.Trim();
        public string Password => _passwordTextBox.Text;

        public RegistrationDialog()
        {
            Text = "Create account";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 240);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var row = 0; row < 4; row++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            var usernameLabel = CreateDialogLabel("Username:");
            var emailLabel = CreateDialogLabel("Email:");
            var passwordLabel = CreateDialogLabel("Password:");
            var confirmPasswordLabel = CreateDialogLabel("Confirm password:");

            ConfigureDialogTextBox(_usernameTextBox, 100);
            ConfigureDialogTextBox(_emailTextBox, 254);
            ConfigureDialogTextBox(_passwordTextBox);
            ConfigureDialogTextBox(_confirmPasswordTextBox);
            _passwordTextBox.UseSystemPasswordChar = true;
            _confirmPasswordTextBox.UseSystemPasswordChar = true;

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Text = "Cancel",
                Width = 90
            };
            var createButton = new Button
            {
                Text = "Create account",
                Width = 115
            };
            createButton.Click += CreateButton_Click;

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = false
            };
            cancelButton.Margin = new Padding(5, 5, 0, 5);
            createButton.Margin = new Padding(5, 5, 0, 5);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(createButton);

            layout.Controls.Add(usernameLabel, 0, 0);
            layout.Controls.Add(_usernameTextBox, 1, 0);
            layout.Controls.Add(emailLabel, 0, 1);
            layout.Controls.Add(_emailTextBox, 1, 1);
            layout.Controls.Add(passwordLabel, 0, 2);
            layout.Controls.Add(_passwordTextBox, 1, 2);
            layout.Controls.Add(confirmPasswordLabel, 0, 3);
            layout.Controls.Add(_confirmPasswordTextBox, 1, 3);
            layout.Controls.Add(buttonPanel, 0, 4);
            layout.SetColumnSpan(buttonPanel, 2);

            Controls.Add(layout);
            AcceptButton = createButton;
            CancelButton = cancelButton;
        }

        private void CreateButton_Click(object? sender, EventArgs e)
        {
            if (Username.Length < 2)
            {
                MessageBox.Show(this, "Username must contain at least 2 characters.", "Invalid username");
                _usernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
            {
                MessageBox.Show(this, "Enter a valid email address.", "Invalid email");
                _emailTextBox.Focus();
                return;
            }

            if (Password.Length < 6)
            {
                MessageBox.Show(this, "Password must contain at least 6 characters.", "Invalid password");
                _passwordTextBox.Focus();
                return;
            }

            if (!string.Equals(Password, _confirmPasswordTextBox.Text, StringComparison.Ordinal))
            {
                MessageBox.Show(this, "The passwords do not match.", "Invalid password");
                _confirmPasswordTextBox.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private static Label CreateDialogLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static void ConfigureDialogTextBox(TextBox textBox, int maxLength = 128)
        {
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(3, 5, 3, 5);
            textBox.MaxLength = maxLength;
        }
    }

}
