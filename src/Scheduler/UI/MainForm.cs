using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

public sealed class MainForm : Form
{
    private readonly JobService _jobService;

    private readonly TabControl _tabControl;
    private readonly ListView _jobListView;
    private readonly GanttChartPanel _ganttChartPanel;
    private readonly ComboBox _sortComboBox;
    private readonly TextBox _nameTextBox;
    private readonly MonthCalendar _startCalendar;
    private readonly MonthCalendar _endCalendar;
    private readonly Button _addButton;
    private readonly Button _deleteButton;

    public MainForm(JobService jobService)
    {
        _jobService = jobService;

        Text = "Scheduler";
        Width = 640;
        Height = 660;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);

        _jobListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        _jobListView.Columns.Add("Name", 250);
        _jobListView.Columns.Add("Start Date", 150);
        _jobListView.Columns.Add("End Date", 150);

        _ganttChartPanel = new GanttChartPanel
        {
            Dock = DockStyle.Fill
        };

        var listTabPage = new TabPage("List");
        listTabPage.Controls.Add(_jobListView);

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
        _addButton = new Button { Text = "Add Job", AutoSize = true };
        _addButton.Click += AddButton_Click;

        _deleteButton = new Button { Text = "Delete Job", AutoSize = true, Enabled = false };
        _deleteButton.Click += DeleteButton_Click;

        _jobListView.SelectedIndexChanged += (_, _) => _deleteButton.Enabled = _jobListView.SelectedItems.Count > 0;

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
        topPanel.Controls.Add(_addButton);
        topPanel.Controls.Add(_deleteButton);

        var datePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 8)
        };
        datePanel.Controls.Add(BuildLabeledCalendar("Start", _startCalendar));
        datePanel.Controls.Add(BuildLabeledCalendar("End", _endCalendar));

        Controls.Add(_tabControl);
        Controls.Add(datePanel);
        Controls.Add(topPanel);

        Load += (_, _) => RefreshJobList();
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

    private void AddButton_Click(object? sender, EventArgs e)
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

        _jobService.AddJob(new Job
        {
            Name = _nameTextBox.Text.Trim(),
            StartDate = _startCalendar.SelectionStart.Date,
            EndDate = _endCalendar.SelectionStart.Date
        });

        _nameTextBox.Clear();
        RefreshJobList();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_jobListView.SelectedItems.Count == 0)
        {
            return;
        }

        var job = (Job)_jobListView.SelectedItems[0].Tag!;
        var confirm = MessageBox.Show(this, $"Delete job \"{job.Name}\"?", "Scheduler",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _jobService.DeleteJob(job.Id);
        RefreshJobList();
    }

    private void RefreshJobList()
    {
        var sortOrder = _sortComboBox.SelectedIndex == 1 ? JobSortOrder.EndDate : JobSortOrder.StartDate;
        var jobs = _jobService.GetJobs(sortOrder);

        _jobListView.Items.Clear();
        foreach (var job in jobs)
        {
            var item = new ListViewItem(job.Name) { Tag = job };
            item.SubItems.Add(job.StartDate.ToShortDateString());
            item.SubItems.Add(job.EndDate.ToShortDateString());
            _jobListView.Items.Add(item);
        }

        _deleteButton.Enabled = _jobListView.SelectedItems.Count > 0;
        _ganttChartPanel.SetJobs(jobs);
    }
}
