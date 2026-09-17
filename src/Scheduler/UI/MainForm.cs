using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    private readonly JobService _jobService;

    private readonly ListView _jobListView;
    private readonly ComboBox _sortComboBox;
    private readonly TextBox _nameTextBox;
    private readonly DateTimePicker _startDatePicker;
    private readonly DateTimePicker _endDatePicker;
    private readonly Button _addButton;

    public MainForm(JobService jobService)
    {
        _jobService = jobService;

        Text = "Scheduler";
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;

        _jobListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        _jobListView.Columns.Add("Name", 250);
        _jobListView.Columns.Add("Start Date", 150);
        _jobListView.Columns.Add("End Date", 150);

        _sortComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _sortComboBox.Items.AddRange(["Start Date", "End Date"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshJobList();

        _nameTextBox = new TextBox { Width = 150, PlaceholderText = "Job name" };
        _startDatePicker = new DateTimePicker { Width = 120 };
        _endDatePicker = new DateTimePicker { Width = 120 };
        _addButton = new Button { Text = "Add Job", AutoSize = true };
        _addButton.Click += AddButton_Click;

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
        topPanel.Controls.Add(new Label { Text = "Start:", AutoSize = true, Margin = new Padding(15, 8, 3, 3) });
        topPanel.Controls.Add(_startDatePicker);
        topPanel.Controls.Add(new Label { Text = "End:", AutoSize = true, Margin = new Padding(15, 8, 3, 3) });
        topPanel.Controls.Add(_endDatePicker);
        topPanel.Controls.Add(_addButton);

        Controls.Add(_jobListView);
        Controls.Add(topPanel);

        Load += (_, _) => RefreshJobList();
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, "Enter a job name.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_endDatePicker.Value.Date < _startDatePicker.Value.Date)
        {
            MessageBox.Show(this, "End date must be on or after the start date.", "Scheduler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _jobService.AddJob(new Job
        {
            Name = _nameTextBox.Text.Trim(),
            StartDate = _startDatePicker.Value.Date,
            EndDate = _endDatePicker.Value.Date
        });

        _nameTextBox.Clear();
        RefreshJobList();
    }

    private void RefreshJobList()
    {
        var sortOrder = _sortComboBox.SelectedIndex == 1 ? JobSortOrder.EndDate : JobSortOrder.StartDate;
        var jobs = _jobService.GetJobs(sortOrder);

        _jobListView.Items.Clear();
        foreach (var job in jobs)
        {
            var item = new ListViewItem(job.Name);
            item.SubItems.Add(job.StartDate.ToShortDateString());
            item.SubItems.Add(job.EndDate.ToShortDateString());
            _jobListView.Items.Add(item);
        }
    }
}
