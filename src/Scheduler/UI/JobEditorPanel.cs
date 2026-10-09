using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

/// <summary>
/// The add/edit form for one job: name, dates, phase and pins, with Save/Cancel/Delete. It saves
/// through <see cref="JobService"/> itself and raises <see cref="JobsChanged"/> so the owner can
/// refresh whatever shows the jobs.
/// </summary>
internal sealed class JobEditorPanel : TableLayoutPanel
{
    private readonly JobService _jobService;

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
    // changing the phase can restamp dates and the form simply reloads from it. Saving stores the
    // draft, updating the job being edited or adding a new one.
    private Job? _editingJob;
    private Job _draft = new();
    private bool _loading;

    /// <summary>Raised after a job was added, updated or deleted.</summary>
    public event Action? JobsChanged;

    public JobEditorPanel(JobService jobService)
    {
        _jobService = jobService;

        Dock = DockStyle.Top;
        AutoSize = true;
        ColumnCount = 1;
        RowCount = 2;

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
        _stageComboBox.SetOptions(
            new ComboOption<JobStage>("No phases", JobStage.NoPhases),
            new ComboOption<JobStage>("Prospect", JobStage.Prospect),
            new ComboOption<JobStage>("Design", JobStage.Design),
            new ComboOption<JobStage>("Construction", JobStage.Construction),
            new ComboOption<JobStage>("Delivery", JobStage.Delivery),
            new ComboOption<JobStage>("Finished", JobStage.Finished));
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
        _cancelButton.Click += (_, _) => EndEdit();

        _deleteButton = new Button { Text = "Delete Job", AutoSize = true, Enabled = false };
        _deleteButton.Click += DeleteButton_Click;

        var namePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };
        namePanel.Controls.Add(new Label { Text = "Name:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        namePanel.Controls.Add(_nameTextBox);
        namePanel.Controls.Add(_saveButton);
        namePanel.Controls.Add(_cancelButton);
        namePanel.Controls.Add(_deleteButton);

        var datePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
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

        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(namePanel, 0, 0);
        Controls.Add(datePanel, 0, 1);

        ResetDraft();
    }

    /// <summary>Id of the job open in the editor, or null when adding a new one.</summary>
    public int? EditingJobId => _editingJob?.Id;

    /// <summary>Loads a copy of the job into the form for editing.</summary>
    public void BeginEdit(Job job)
    {
        _editingJob = job;
        _draft = job.Clone();
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
            MessageBox.Show(FindForm(), "Enter a job name.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReadControlsIntoDraft();

        // Phased jobs get no ordering checks: their dates can be edited freely.
        if (_draft.Phase is null && _draft.EndDate.Date < _draft.StartDate.Date)
        {
            MessageBox.Show(FindForm(), "End date must be on or after the start date.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_editingJob is not null)
        {
            _jobService.UpdateJob(_draft);
        }
        else
        {
            _jobService.AddJob(_draft);
        }

        EndEdit();
        JobsChanged?.Invoke();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_editingJob is not { } job)
        {
            return;
        }

        var confirm = MessageBox.Show(FindForm(), $"Delete job \"{job.Name}\"?", "Scheduler",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _jobService.DeleteJob(job.Id);
        EndEdit();
        JobsChanged?.Invoke();
    }

    // New jobs start as Prospects: placeholders that only become scheduled work later.
    private void ResetDraft()
    {
        var today = DateTime.Today;
        _draft = new Job { Phase = JobPhase.Prospect, StartDate = today, EndDate = today };
        LoadDraftIntoControls();
    }

    private void StageComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ReadControlsIntoDraft();
        _jobService.ChangeStage(_draft, _stageComboBox.GetSelectedValue<JobStage>());
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
        _stageComboBox.SelectValue(JobPhases.GetStage(_draft));

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

        var fields = StageFields.For(JobPhases.GetStage(_draft), _draft.Completed);
        if (fields.HasStartDate)
        {
            _draft.StartDate = _startCalendar.SelectionStart.Date;
        }

        if (fields.HasConstructionStart)
        {
            _draft.ConstructionStartDate = _constructionStartPicker.Value.Date;
        }

        if (fields.HasDeliveryStart)
        {
            _draft.DeliveryStartDate = _deliveryStartPicker.Value.Date;
        }

        if (fields.HasFinishedDate)
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
        var fields = StageFields.For(_stageComboBox.GetSelectedValue<JobStage>(), _completedCheckBox.Checked);

        _completedCheckBox.Visible = !fields.UsesPhases;
        _pinEndCheckBox.Visible = !fields.UsesPhases;
        _endCalendarContainer.Visible = !fields.UsesPhases;
        _phaseDatesPanel.Visible = fields.UsesPhases;

        _pinStartCheckBox.Enabled = fields.CanPinStart;
        _pinEndCheckBox.Enabled = fields.CanPinEnd;

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
        _startCalendar.Enabled = !pinStart && fields.HasStartDate;
        _endCalendar.Enabled = !pinEnd;

        _constructionStartPicker.Enabled = fields.HasConstructionStart;
        _deliveryStartPicker.Enabled = fields.HasDeliveryStart;
        _finishedPicker.Enabled = fields.HasFinishedDate;
    }
}
