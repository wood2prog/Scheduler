using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    private readonly JobService _jobService;

    private readonly GanttChartPanel _ganttChartPanel;
    private readonly ComboBox _sortComboBox;
    private readonly TextBox _nameTextBox;
    private readonly MonthCalendar _startCalendar;
    private readonly MonthCalendar _endCalendar;
    private readonly CheckBox _pinStartCheckBox;
    private readonly CheckBox _pinEndCheckBox;
    private readonly CheckBox _completedCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _deleteButton;
    private Job? _editingJob;

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

        _ganttChartPanel = new GanttChartPanel
        {
            Dock = DockStyle.Fill
        };
        _ganttChartPanel.JobClicked += BeginEdit;

        _sortComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _sortComboBox.Items.AddRange(["Start Date", "End Date", "Name"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshJobList();

        _nameTextBox = new TextBox { Width = 150, PlaceholderText = "Job name" };
        _startCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _endCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _pinStartCheckBox = new CheckBox { Text = "Pin start to today", AutoSize = true };
        _pinStartCheckBox.CheckedChanged += (_, _) => UpdateDateControls();
        _pinEndCheckBox = new CheckBox { Text = "Pin end to today", AutoSize = true };
        _pinEndCheckBox.CheckedChanged += (_, _) => UpdateDateControls();
        _completedCheckBox = new CheckBox { Text = "Completed", AutoSize = true };
        _completedCheckBox.CheckedChanged += (_, _) => UpdateDateControls();
        _saveButton = new Button { Text = "Add Job", AutoSize = true };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button { Text = "Cancel", AutoSize = true, Enabled = false };
        _cancelButton.Click += CancelButton_Click;

        _deleteButton = new Button { Text = "Delete Job", AutoSize = true, Enabled = false };
        _deleteButton.Click += DeleteButton_Click;

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8)
        };
        topPanel.Controls.Add(new Label { Text = "Sort by:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        topPanel.Controls.Add(_sortComboBox);
        topPanel.Controls.Add(new Label { Text = "Name:", AutoSize = true, Margin = new Padding(15, 8, 3, 3) });
        topPanel.Controls.Add(_nameTextBox);
        topPanel.Controls.Add(_saveButton);
        topPanel.Controls.Add(_cancelButton);
        topPanel.Controls.Add(_deleteButton);

        var datePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 8)
        };
        datePanel.Controls.Add(BuildLabeledCalendar("Start", _startCalendar));
        datePanel.Controls.Add(BuildLabeledCalendar("End", _endCalendar));
        var optionsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0, 22, 0, 0)
        };
        optionsPanel.Controls.Add(_pinStartCheckBox);
        optionsPanel.Controls.Add(_pinEndCheckBox);
        optionsPanel.Controls.Add(_completedCheckBox);
        datePanel.Controls.Add(optionsPanel);

        Controls.Add(_ganttChartPanel);
        Controls.Add(datePanel);
        Controls.Add(topPanel);

        Load += (_, _) => RefreshJobList();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        WindowSettings.Save(WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
    }

    private static Control BuildLabeledCalendar(string label, MonthCalendar calendar)
    {
        var container = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0, 0, 15, 0)
        };
        container.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 0, 3, 3) });
        container.Controls.Add(calendar);
        return container;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, "Enter a job name.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_endCalendar.SelectionStart.Date < _startCalendar.SelectionStart.Date)
        {
            MessageBox.Show(this, "End date must be on or after the start date.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_editingJob is { } job)
        {
            job.Name = _nameTextBox.Text.Trim();
            job.StartDate = _startCalendar.SelectionStart.Date;
            job.EndDate = _endCalendar.SelectionStart.Date;
            job.Completed = _completedCheckBox.Checked;
            job.PinStartToToday = _pinStartCheckBox.Checked;
            job.PinEndToToday = _pinEndCheckBox.Checked;
            _jobService.UpdateJob(job);
        }
        else
        {
            _jobService.AddJob(new Job
            {
                Name = _nameTextBox.Text.Trim(),
                StartDate = _startCalendar.SelectionStart.Date,
                EndDate = _endCalendar.SelectionStart.Date,
                Completed = _completedCheckBox.Checked,
                PinStartToToday = _pinStartCheckBox.Checked,
                PinEndToToday = _pinEndCheckBox.Checked
            });
        }

        EndEdit();
        RefreshJobList();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        EndEdit();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_editingJob is not { } job)
        {
            return;
        }

        var confirm = MessageBox.Show(this, $"Delete job \"{job.Name}\"?", "Scheduler",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _jobService.DeleteJob(job.Id);
        EndEdit();
        RefreshJobList();
    }

    private void BeginEdit(Job job)
    {
        _editingJob = job;
        _nameTextBox.Text = job.Name;
        _startCalendar.SelectionStart = job.StartDate;
        _startCalendar.SelectionEnd = job.StartDate;
        _endCalendar.SelectionStart = job.EndDate;
        _endCalendar.SelectionEnd = job.EndDate;
        _completedCheckBox.Checked = job.Completed;
        _pinStartCheckBox.Checked = job.PinStartToToday;
        _pinEndCheckBox.Checked = job.PinEndToToday;
        UpdateDateControls();
        _saveButton.Text = "Save";
        _cancelButton.Enabled = true;
        _deleteButton.Enabled = true;
    }

    private void EndEdit()
    {
        _editingJob = null;
        _nameTextBox.Clear();
        _completedCheckBox.Checked = false;
        _pinStartCheckBox.Checked = false;
        _pinEndCheckBox.Checked = false;
        UpdateDateControls();
        _saveButton.Text = "Add Job";
        _cancelButton.Enabled = false;
        _deleteButton.Enabled = false;
    }

    // A pinned date follows today, so its calendar is snapped to today and locked. Once the job
    // is completed the pins stop applying: the dates it has at that point are kept, and the pin
    // checkboxes are disabled (their state is preserved in case the job is reopened).
    private void UpdateDateControls()
    {
        var completed = _completedCheckBox.Checked;
        _pinStartCheckBox.Enabled = !completed;
        _pinEndCheckBox.Enabled = !completed;

        var pinStart = _pinStartCheckBox.Checked && !completed;
        var pinEnd = _pinEndCheckBox.Checked && !completed;
        var today = DateTime.Today;

        if (pinStart)
        {
            _startCalendar.SetDate(today);
        }

        if (pinEnd)
        {
            _endCalendar.SetDate(today);
        }

        _startCalendar.Enabled = !pinStart;
        _endCalendar.Enabled = !pinEnd;
    }

    private JobSortOrder GetSelectedSortOrder() => _sortComboBox.SelectedIndex switch
    {
        1 => JobSortOrder.EndDate,
        2 => JobSortOrder.Name,
        _ => JobSortOrder.StartDate
    };

    private void RefreshJobList()
    {
        var jobs = _jobService.GetJobs(GetSelectedSortOrder());
        _ganttChartPanel.SetJobs(jobs);
    }
}
