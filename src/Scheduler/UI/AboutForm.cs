using System.Diagnostics;
using System.Reflection;

namespace Scheduler.UI;

/// <summary>Small "About" dialog: logo, version, a short description and a link to the repository.</summary>
internal sealed class AboutForm : Form
{
    private const string RepositoryUrl = "https://github.com/wood2prog/Scheduler";

    public AboutForm()
    {
        Text = "About Scheduler";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 250);

        var logo = new PictureBox
        {
            Image = AppLogo.Load(96),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 20),
            Size = new Size(96, 96)
        };

        var title = new Label
        {
            Text = "Scheduler",
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(130, 20)
        };

        var version = new Label
        {
            Text = $"Version {GetVersion()}",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(132, 54)
        };

        var description = new Label
        {
            Text = "Simple project planning for real work: schedule jobs on a Gantt chart, move them " +
                   "through Prospect, Design, Construction and Delivery, and see how long jobs take.",
            Location = new Point(132, 80),
            Size = new Size(248, 90)
        };

        var link = new LinkLabel
        {
            Text = RepositoryUrl,
            AutoSize = true,
            Location = new Point(20, 150)
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                MessageBox.Show(this, $"Could not open the link. The address is {RepositoryUrl}", "Scheduler",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(305, 205),
            Size = new Size(75, 28)
        };

        Controls.AddRange([logo, title, version, description, link, ok]);
        AcceptButton = ok;
        CancelButton = ok;
    }

    // The three-part version from Scheduler.csproj (the SDK would otherwise append a commit hash).
    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Controls.OfType<PictureBox>().FirstOrDefault()?.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
