using Scheduler.Application;

namespace Scheduler.UI;

/// <summary>Pop-up window that hosts a generated report chart.</summary>
public sealed class ReportForm : Form
{
    public ReportForm(DurationReport report)
    {
        Text = $"Report - {report.Title}";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        ShowIcon = false;
        ShowInTaskbar = false;

        Controls.Add(new DurationReportPanel(report) { Dock = DockStyle.Fill });
    }
}
