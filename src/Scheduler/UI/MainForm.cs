using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    /// <summary>A report on one phase's lengths, or on whole jobs when <paramref name="Phase"/> is null.</summary>
    /// <param name="Missing">What there must be enough of, for the "not enough data" message.</param>
    private sealed record ReportKind(JobPhase? Phase, string Missing);

    private readonly JobService _jobService;

    private readonly JobEditorPanel _editor;
    private readonly GanttChartPanel _ganttChartPanel;
    private readonly ComboBox _sortComboBox;
    private readonly CheckBox _hideCompletedCheckBox;
    private readonly ComboBox _reportComboBox;

    public MainForm(JobService jobService)
    {
        _jobService = jobService;

        Text = "Scheduler";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);

        if (WindowSettings.Load() is { } savedBounds)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = savedBounds;
        }
        else
        {
            Width = 640;
            Height = 660;
            StartPosition = FormStartPosition.CenterScreen;
        }

        _editor = new JobEditorPanel(jobService);
        _editor.JobsChanged += RefreshJobList;

        _ganttChartPanel = new GanttChartPanel
        {
            Dock = DockStyle.Fill
        };
        _ganttChartPanel.JobClicked += _editor.BeginEdit;
        _ganttChartPanel.SeamMoved += GanttChartPanel_SeamMoved;

        _sortComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _sortComboBox.SetOptions(
            new ComboOption<JobSortOrder>("Start Date", JobSortOrder.StartDate),
            new ComboOption<JobSortOrder>("End Date", JobSortOrder.EndDate),
            new ComboOption<JobSortOrder>("Name", JobSortOrder.Name));
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshJobList();

        _hideCompletedCheckBox = new CheckBox { Text = "Hide completed", AutoSize = true, Margin = new Padding(15, 6, 3, 3) };
        _hideCompletedCheckBox.CheckedChanged += (_, _) => RefreshJobList();

        _reportComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180
        };
        _reportComboBox.SetOptions(
            new ComboOption<ReportKind>("Completed Job Durations", new ReportKind(null, "completed jobs")),
            new ComboOption<ReportKind>("Design Phase Durations", new ReportKind(JobPhase.Design, "jobs that have finished Design")),
            new ComboOption<ReportKind>("Construction Phase Durations", new ReportKind(JobPhase.Construction, "jobs that have finished Construction")),
            new ComboOption<ReportKind>("Delivery Phase Durations", new ReportKind(JobPhase.Delivery, "jobs that have finished Delivery")));

        var generateReportButton = new Button { Text = "Generate Report", AutoSize = true };
        generateReportButton.Click += GenerateReportButton_Click;

        // How the chart is viewed (order, filter) and the reports; editing lives in the editor above.
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 4)
        };
        toolbar.Controls.Add(new Label { Text = "Sort by:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        toolbar.Controls.Add(_sortComboBox);
        toolbar.Controls.Add(_hideCompletedCheckBox);
        toolbar.Controls.Add(new Label { Text = "Reports:", AutoSize = true, Margin = new Padding(15, 8, 3, 3) });
        toolbar.Controls.Add(_reportComboBox);
        toolbar.Controls.Add(generateReportButton);

        // Docked controls stack in reverse add order: the editor ends up on top.
        Controls.Add(_ganttChartPanel);
        Controls.Add(toolbar);
        Controls.Add(_editor);

        Load += (_, _) => RefreshJobList();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        WindowSettings.Save(WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
    }

    private void GenerateReportButton_Click(object? sender, EventArgs e)
    {
        var kind = _reportComboBox.GetSelectedValue<ReportKind>();
        var report = kind.Phase is { } phase
            ? _jobService.GetPhaseDurationReport(phase)
            : _jobService.GetDurationReport();
        if (report is null)
        {
            MessageBox.Show(this,
                $"At least {DurationReport.MinimumJobs} {kind.Missing} are needed to generate this report.",
                "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ReportForm(report);
        form.ShowDialog(this);
    }

    private void GanttChartPanel_SeamMoved(Job job, JobPhase phase, DateTime date)
    {
        _jobService.MovePhaseStart(job, phase, date);
        RefreshJobList();

        // If this job is open in the editor, reload it so the phase date pickers match the chart.
        if (_editor.EditingJobId == job.Id)
        {
            _editor.BeginEdit(job);
        }
    }

    private void RefreshJobList()
    {
        var jobs = _jobService.GetJobs(_sortComboBox.GetSelectedValue<JobSortOrder>(),
            includeCompleted: !_hideCompletedCheckBox.Checked);
        _ganttChartPanel.SetJobs(jobs, _jobService.GetSegments);
    }
}
