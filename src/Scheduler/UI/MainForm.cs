using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    private readonly JobService _jobService;

    private readonly TabControl _tabControl;
    private readonly DataGridView _jobGridView;
    private readonly GanttChartPanel _ganttChartPanel;
    private readonly ComboBox _sortComboBox;
    private readonly TextBox _nameTextBox;
    private readonly MonthCalendar _startCalendar;
    private readonly MonthCalendar _endCalendar;
    private readonly CheckBox _completedCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _deleteButton;
    private bool _isPopulatingGrid;
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

        _jobGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        _jobGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", ReadOnly = true, FillWeight = 45 });
        _jobGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", HeaderText = "Start Date", ReadOnly = true, FillWeight = 20 });
        _jobGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "End Date", ReadOnly = true, FillWeight = 20 });
        _jobGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Completed", HeaderText = "Completed", FillWeight = 15 });
        _jobGridView.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_jobGridView.CurrentCell is DataGridViewCheckBoxCell)
            {
                _jobGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _jobGridView.CellValueChanged += JobGridView_CellValueChanged;

        _ganttChartPanel = new GanttChartPanel
        {
            Dock = DockStyle.Fill
        };
        _ganttChartPanel.JobClicked += job =>
        {
            _jobGridView.ClearSelection();
            BeginEdit(job);
        };

        var listTabPage = new TabPage("List");
        listTabPage.Controls.Add(_jobGridView);

        var calendarTabPage = new TabPage("Calendar");
        calendarTabPage.Controls.Add(_ganttChartPanel);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };
        _tabControl.TabPages.Add(listTabPage);
        _tabControl.TabPages.Add(calendarTabPage);

        _sortComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _sortComboBox.Items.AddRange(["Start Date", "End Date"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshJobList();

        _nameTextBox = new TextBox { Width = 150, PlaceholderText = "Job name" };
        _startCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _endCalendar = new MonthCalendar { MaxSelectionCount = 1 };
        _completedCheckBox = new CheckBox { Text = "Completed", AutoSize = true, Margin = new Padding(3, 25, 3, 3) };
        _saveButton = new Button { Text = "Add Job", AutoSize = true };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button { Text = "Cancel", AutoSize = true, Enabled = false };
        _cancelButton.Click += CancelButton_Click;

        _deleteButton = new Button { Text = "Delete Job", AutoSize = true, Enabled = false };
        _deleteButton.Click += DeleteButton_Click;

        _jobGridView.SelectionChanged += JobGridView_SelectionChanged;

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
        datePanel.Controls.Add(_completedCheckBox);

        Controls.Add(_tabControl);
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
            _jobService.UpdateJob(job);
        }
        else
        {
            _jobService.AddJob(new Job
            {
                Name = _nameTextBox.Text.Trim(),
                StartDate = _startCalendar.SelectionStart.Date,
                EndDate = _endCalendar.SelectionStart.Date,
                Completed = _completedCheckBox.Checked
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
        if (_jobGridView.SelectedRows.Count == 0)
        {
            return;
        }

        var job = (Job)_jobGridView.SelectedRows[0].Tag!;
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

    private void JobGridView_SelectionChanged(object? sender, EventArgs e)
    {
        _deleteButton.Enabled = _jobGridView.SelectedRows.Count > 0;

        if (_isPopulatingGrid || _jobGridView.SelectedRows.Count == 0)
        {
            return;
        }

        var job = (Job)_jobGridView.SelectedRows[0].Tag!;
        BeginEdit(job);
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
        _saveButton.Text = "Save";
        _cancelButton.Enabled = true;
    }

    private void EndEdit()
    {
        _editingJob = null;
        _nameTextBox.Clear();
        _completedCheckBox.Checked = false;
        _saveButton.Text = "Add Job";
        _cancelButton.Enabled = false;
        _jobGridView.ClearSelection();
    }

    private void JobGridView_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_isPopulatingGrid || e.RowIndex < 0 || _jobGridView.Columns[e.ColumnIndex].Name != "Completed")
        {
            return;
        }

        var row = _jobGridView.Rows[e.RowIndex];
        if (row.Tag is not Job job)
        {
            return;
        }

        var completed = (bool)(row.Cells["Completed"].Value ?? false);
        job.Completed = completed;
        _jobService.SetCompleted(job.Id, completed);

        var sortOrder = _sortComboBox.SelectedIndex == 1 ? JobSortOrder.EndDate : JobSortOrder.StartDate;
        _ganttChartPanel.SetJobs(_jobService.GetJobs(sortOrder));
    }

    private void RefreshJobList()
    {
        var sortOrder = _sortComboBox.SelectedIndex == 1 ? JobSortOrder.EndDate : JobSortOrder.StartDate;
        var jobs = _jobService.GetJobs(sortOrder);

        _isPopulatingGrid = true;
        try
        {
            _jobGridView.Rows.Clear();
            foreach (var job in jobs)
            {
                var rowIndex = _jobGridView.Rows.Add(job.Name, job.StartDate.ToShortDateString(), job.EndDate.ToShortDateString(), job.Completed);
                _jobGridView.Rows[rowIndex].Tag = job;
            }
        }
        finally
        {
            _isPopulatingGrid = false;
        }

        _deleteButton.Enabled = _jobGridView.SelectedRows.Count > 0;
        _ganttChartPanel.SetJobs(jobs);
    }
}
