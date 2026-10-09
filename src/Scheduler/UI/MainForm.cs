using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    private readonly JobService _jobService;

    private readonly GanttChartPanel _ganttChartPanel;
    private readonly ComboBox _sortComboBox;
    private readonly CheckBox _hideCompletedCheckBox;
    private readonly ComboBox _reportComboBox;
    private readonly TextBox _nameTextBox;
    private readonly MonthCalendar _startCalendar;
    private readonly MonthCalendar _endCalendar;
    private readonly CheckBox _pinStartCheckBox;
    private readonly CheckBox _pinEndCheckBox;
    private readonly CheckBox _completedCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _deleteButton;
    private readonly ComboBox _stageComboBox;
    private readonly Control _endCalendarContainer;
    private readonly TableLayoutPanel _phaseDatesPanel;
    private readonly DateTimePicker _constructionStartPicker;
    private readonly DateTimePicker _deliveryStartPicker;
    private readonly DateTimePicker _finishedPicker;
    private readonly DateTimePicker _deliveryTargetPicker;

    // The job being edited (null when adding). All form fields work on _draft, a copy, so that
    // changing the phase can restamp dates and the form simply reloads from it. Saving copies
    // the draft back to _editingJob (or adds it as a new job).
    private Job? _editingJob;
    private Job _draft = new();
    private bool _loading;

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
        _ganttChartPanel.SeamMoved += GanttChartPanel_SeamMoved;

        _sortComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _sortComboBox.Items.AddRange(["Start Date", "End Date", "Name"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshJobList();

        _hideCompletedCheckBox = new CheckBox { Text = "Hide completed", AutoSize = true, Margin = new Padding(15, 6, 3, 3) };
        _hideCompletedCheckBox.CheckedChanged += (_, _) => RefreshJobList();

        _nameTextBox = new TextBox { Width = 150, PlaceholderText = "Job name" };
        _startCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _endCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _pinStartCheckBox = new CheckBox { Text = "Pin start to today", AutoSize = true };
        _pinStartCheckBox.CheckedChanged += (_, _) => UpdateDateControls();
        _pinEndCheckBox = new CheckBox { Text = "Pin end to today", AutoSize = true };
        _pinEndCheckBox.CheckedChanged += (_, _) => UpdateDateControls();
        _completedCheckBox = new CheckBox { Text = "Completed", AutoSize = true };
        _completedCheckBox.CheckedChanged += (_, _) => UpdateDateControls();

        _stageComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Margin = new Padding(3, 0, 3, 8)
        };
        _stageComboBox.Items.AddRange(["No phases", "Prospect", "Design", "Construction", "Delivery", "Finished"]);
        _stageComboBox.SelectedIndex = 0;
        _stageComboBox.SelectedIndexChanged += StageComboBox_SelectedIndexChanged;

        _constructionStartPicker = CreateDatePicker();
        _deliveryStartPicker = CreateDatePicker();
        _finishedPicker = CreateDatePicker();
        _deliveryTargetPicker = CreateDatePicker();
        _deliveryTargetPicker.ShowCheckBox = true;

        // Stands in for the End calendar while a job uses phases.
        _phaseDatesPanel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 15, 0),
            Visible = false
        };
        var phaseDatesTitle = new Label { Text = "Phase dates", AutoSize = true, Margin = new Padding(3, 0, 3, 3) };
        _phaseDatesPanel.Controls.Add(phaseDatesTitle, 0, 0);
        _phaseDatesPanel.SetColumnSpan(phaseDatesTitle, 2);
        AddPhaseDateRow(1, "Construction starts", _constructionStartPicker);
        AddPhaseDateRow(2, "Delivery starts", _deliveryStartPicker);
        AddPhaseDateRow(3, "Finished on", _finishedPicker);
        AddPhaseDateRow(4, "Delivery target", _deliveryTargetPicker);

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
        topPanel.Controls.Add(_hideCompletedCheckBox);
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
        _endCalendarContainer = BuildLabeledCalendar("End", _endCalendar);
        datePanel.Controls.Add(_endCalendarContainer);
        datePanel.Controls.Add(_phaseDatesPanel);
        var optionsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0)
        };
        optionsPanel.Controls.Add(new Label { Text = "Phase", AutoSize = true, Margin = new Padding(3, 0, 3, 3) });
        optionsPanel.Controls.Add(_stageComboBox);
        optionsPanel.Controls.Add(_pinStartCheckBox);
        optionsPanel.Controls.Add(_pinEndCheckBox);
        optionsPanel.Controls.Add(_completedCheckBox);
        datePanel.Controls.Add(optionsPanel);

        _reportComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Margin = new Padding(3, 0, 3, 3)
        };
        _reportComboBox.Items.AddRange(["Completed Job Durations", "Design Phase Durations",
            "Construction Phase Durations", "Delivery Phase Durations"]);
        _reportComboBox.SelectedIndex = 0;

        var generateReportButton = new Button { Text = "Generate Report", AutoSize = true };
        generateReportButton.Click += GenerateReportButton_Click;

        // Reports sit under the checkboxes in the same column, so the form keeps its original
        // wide, short shape instead of growing a new column.
        optionsPanel.Controls.Add(new Label { Text = "Reports", AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
        optionsPanel.Controls.Add(_reportComboBox);
        optionsPanel.Controls.Add(generateReportButton);

        Controls.Add(_ganttChartPanel);
        Controls.Add(datePanel);
        Controls.Add(topPanel);

        ResetDraft();

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

    private static DateTimePicker CreateDatePicker() =>
        new() { Format = DateTimePickerFormat.Short, Width = 110, Margin = new Padding(3, 3, 3, 6) };

    private void AddPhaseDateRow(int row, string label, DateTimePicker picker)
    {
        _phaseDatesPanel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, row);
        _phaseDatesPanel.Controls.Add(picker, 1, row);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, "Enter a job name.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReadControlsIntoDraft();

        // Phased jobs get no ordering checks: their dates can be edited freely.
        if (_draft.Phase is null && _draft.EndDate.Date < _draft.StartDate.Date)
        {
            MessageBox.Show(this, "End date must be on or after the start date.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_editingJob is { } job)
        {
            CopyJob(_draft, job);
            _jobService.UpdateJob(job);
        }
        else
        {
            _jobService.AddJob(_draft);
        }

        EndEdit();
        RefreshJobList();
    }

    private void GenerateReportButton_Click(object? sender, EventArgs e)
    {
        // Items are in the order added to _reportComboBox.
        var (report, missing) = _reportComboBox.SelectedIndex switch
        {
            1 => (_jobService.GetPhaseDurationReport(JobPhase.Design), "jobs that have finished Design"),
            2 => (_jobService.GetPhaseDurationReport(JobPhase.Construction), "jobs that have finished Construction"),
            3 => (_jobService.GetPhaseDurationReport(JobPhase.Delivery), "jobs that have finished Delivery"),
            _ => (_jobService.GetDurationReport(), "completed jobs")
        };
        if (report is null)
        {
            MessageBox.Show(this,
                $"At least {DurationReport.MinimumJobs} {missing} are needed to generate this report.",
                "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ReportForm(report);
        form.ShowDialog(this);
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

    private void GanttChartPanel_SeamMoved(Job job, JobPhase phase, DateTime date)
    {
        _jobService.MovePhaseStart(job, phase, date);
        RefreshJobList();

        // If this job is open in the form, reload it so the phase date pickers match the chart.
        if (_editingJob?.Id == job.Id)
        {
            BeginEdit(job);
        }
    }

    private void BeginEdit(Job job)
    {
        _editingJob = job;
        _draft = new Job();
        CopyJob(job, _draft);
        LoadDraftIntoControls();
        _saveButton.Text = "Save";
        _cancelButton.Enabled = true;
        _deleteButton.Enabled = true;
    }

    private void EndEdit()
    {
        _editingJob = null;
        ResetDraft();
        _saveButton.Text = "Add Job";
        _cancelButton.Enabled = false;
        _deleteButton.Enabled = false;
    }

    // New jobs start as Prospects: placeholders that only become scheduled work later.
    private void ResetDraft()
    {
        var today = DateTime.Today;
        _draft = new Job { Phase = JobPhase.Prospect, StartDate = today, EndDate = today };
        LoadDraftIntoControls();
    }

    private static void CopyJob(Job from, Job to)
    {
        to.Id = from.Id;
        to.Name = from.Name;
        to.StartDate = from.StartDate;
        to.EndDate = from.EndDate;
        to.Completed = from.Completed;
        to.PinStartToToday = from.PinStartToToday;
        to.PinEndToToday = from.PinEndToToday;
        to.Phase = from.Phase;
        to.ConstructionStartDate = from.ConstructionStartDate;
        to.DeliveryStartDate = from.DeliveryStartDate;
        to.DeliveryTargetDate = from.DeliveryTargetDate;
    }

    private void StageComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ReadControlsIntoDraft();
        _jobService.ChangeStage(_draft, (JobStage)_stageComboBox.SelectedIndex);
        LoadDraftIntoControls();
    }

    private void LoadDraftIntoControls()
    {
        _loading = true;
        var today = DateTime.Today;

        _nameTextBox.Text = _draft.Name;
        _startCalendar.SetDate(_draft.StartDate);
        _endCalendar.SetDate(_draft.EndDate);
        _pinStartCheckBox.Checked = _draft.PinStartToToday;
        _pinEndCheckBox.Checked = _draft.PinEndToToday;
        _completedCheckBox.Checked = _draft.Completed;
        _stageComboBox.SelectedIndex = (int)JobService.GetStage(_draft);

        _constructionStartPicker.Value = (_draft.ConstructionStartDate ?? today).Date;
        _deliveryStartPicker.Value = (_draft.DeliveryStartDate ?? today).Date;
        _finishedPicker.Value = (_draft.Phase is not null && _draft.Completed ? _draft.EndDate : today).Date;
        _deliveryTargetPicker.Value = (_draft.DeliveryTargetDate ?? today).Date;
        _deliveryTargetPicker.Checked = _draft.DeliveryTargetDate is not null;

        _loading = false;
        UpdateDateControls();
    }

    // Copies what the user has entered into _draft. Phase and Completed of a phased job are only
    // changed through the Phase dropdown, never read from here.
    private void ReadControlsIntoDraft()
    {
        _draft.Name = _nameTextBox.Text.Trim();
        _draft.PinStartToToday = _pinStartCheckBox.Checked;
        _draft.PinEndToToday = _pinEndCheckBox.Checked;

        if (_draft.Phase is null)
        {
            _draft.StartDate = _startCalendar.SelectionStart.Date;
            _draft.EndDate = _endCalendar.SelectionStart.Date;
            _draft.Completed = _completedCheckBox.Checked;
            return;
        }

        var stage = JobService.GetStage(_draft);
        if (stage != JobStage.Prospect)
        {
            _draft.StartDate = _startCalendar.SelectionStart.Date;
        }

        if (stage >= JobStage.Construction)
        {
            _draft.ConstructionStartDate = _constructionStartPicker.Value.Date;
        }

        if (stage >= JobStage.Delivery)
        {
            _draft.DeliveryStartDate = _deliveryStartPicker.Value.Date;
        }

        if (stage == JobStage.Finished)
        {
            _draft.EndDate = _finishedPicker.Value.Date;
        }

        _draft.DeliveryTargetDate = _deliveryTargetPicker.Checked ? _deliveryTargetPicker.Value.Date : null;
    }

    // A pinned date follows today, so its calendar is snapped to today and locked. Once an
    // unphased job is completed the pins stop applying: the dates it has at that point are kept,
    // and the pin checkboxes are disabled (their state is preserved in case the job is reopened).
    // Phased jobs have no end pin (they run to today by themselves) and their start can only be
    // pinned while still in Prospect or Design; the phase date pickers enable as phases are reached.
    private void UpdateDateControls()
    {
        var stage = (JobStage)Math.Max(0, _stageComboBox.SelectedIndex);
        var phased = stage != JobStage.NoPhases;
        var unphasedDone = !phased && _completedCheckBox.Checked;

        _completedCheckBox.Visible = !phased;
        _pinEndCheckBox.Visible = !phased;
        _endCalendarContainer.Visible = !phased;
        _phaseDatesPanel.Visible = phased;

        _pinStartCheckBox.Enabled = !unphasedDone && stage is JobStage.NoPhases or JobStage.Prospect or JobStage.Design;
        _pinEndCheckBox.Enabled = !unphasedDone;

        var pinStart = _pinStartCheckBox.Checked && _pinStartCheckBox.Enabled;
        var pinEnd = _pinEndCheckBox.Checked && _pinEndCheckBox.Enabled;
        var today = DateTime.Today;

        if (pinStart)
        {
            _startCalendar.SetDate(today);
        }

        if (pinEnd)
        {
            _endCalendar.SetDate(today);
        }

        // A Prospect has no start yet; it is set when the job moves into Design.
        _startCalendar.Enabled = !pinStart && stage != JobStage.Prospect;
        _endCalendar.Enabled = !pinEnd;

        _constructionStartPicker.Enabled = stage >= JobStage.Construction;
        _deliveryStartPicker.Enabled = stage >= JobStage.Delivery;
        _finishedPicker.Enabled = stage == JobStage.Finished;
    }

    private JobSortOrder GetSelectedSortOrder() => _sortComboBox.SelectedIndex switch
    {
        1 => JobSortOrder.EndDate,
        2 => JobSortOrder.Name,
        _ => JobSortOrder.StartDate
    };

    private void RefreshJobList()
    {
        var jobs = _jobService.GetJobs(GetSelectedSortOrder(), includeCompleted: !_hideCompletedCheckBox.Checked);
        _ganttChartPanel.SetJobs(jobs, _jobService.GetSegments);
    }
}
