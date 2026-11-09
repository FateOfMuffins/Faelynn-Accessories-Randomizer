using System;
using System.Drawing;
using System.Windows.Forms;

namespace FaelynnRandomizer
{
    internal sealed class SetupForm : Form
    {
        private readonly Installation installation;
        private readonly NumericUpDown delay;
        private readonly CheckBox startAtSignIn;
        private readonly Label status;
        private readonly Button apply, uninstall;

        internal SetupForm(Installation installation)
        {
            this.installation = installation;
            Text = Installation.Product;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(490, 250);

            var layout = new TableLayoutPanel {
                ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(22, 17, 22, 14)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(layout);

            var heading = new Label {
                Text = Installation.Product, Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            Span(layout, heading, 0);
            layout.Controls.Add(new Label {
                Text = "Delay after GF2 launches (seconds)", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);
            delay = new NumericUpDown {
                Minimum = Installation.MinDelay, Maximum = Installation.MaxDelay, Value = installation.ReadDelay(),
                Width = 83, Anchor = AnchorStyles.Right, AccessibleName = "Launch delay in seconds"
            };
            layout.Controls.Add(delay, 1, 1);

            startAtSignIn = new CheckBox {
                Text = "Start at Windows sign-in", Checked = !installation.IsInstalled || installation.StartsAtSignIn,
                AutoSize = true, Anchor = AnchorStyles.Left
            };
            Span(layout, startAtSignIn, 2);
            status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            Span(layout, status, 3);

            var actions = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false
            };
            apply = new Button { Width = 116, Height = 32 };
            apply.Click += Save;
            var close = new Button { Text = "Close", Width = 76, Height = 32, DialogResult = DialogResult.Cancel };
            uninstall = new Button { Text = "Uninstall", Width = 89, Height = 32 };
            uninstall.Click += Remove;
            actions.Controls.AddRange(new Control[] { apply, close, uninstall });
            Span(layout, actions, 4);
            AcceptButton = apply;
            CancelButton = close;
            RefreshStatus(installation.IsInstalled ? "Installed for this Windows account." : "Not installed.");
        }

        private static void Span(TableLayoutPanel layout, Control control, int row)
        {
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
        }

        private void Save(object sender, EventArgs args)
        {
            try
            {
                bool wasInstalled = installation.IsInstalled;
                installation.Apply((int)delay.Value, startAtSignIn.Checked);
                RefreshStatus(wasInstalled ? "Saved. The new delay applies to the next launch." : "Installed. The watcher is running.");
            }
            catch (Exception error) { ShowError(error); }
        }

        private void Remove(object sender, EventArgs args)
        {
            if (MessageBox.Show(this, "Remove the randomizer and its sign-in shortcut?", Installation.Product,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                if (installation.Remove()) { Close(); return; }
                RefreshStatus("Uninstalled.");
            }
            catch (Exception error) { ShowError(error); }
        }

        private void RefreshStatus(string message)
        {
            status.Text = message;
            apply.Text = installation.IsInstalled ? "Save settings" : "Install";
            uninstall.Enabled = installation.IsInstalled;
        }

        private void ShowError(Exception error)
        {
            MessageBox.Show(this, error.Message, Installation.Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
