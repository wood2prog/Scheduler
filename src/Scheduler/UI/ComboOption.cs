namespace Scheduler.UI;

/// <summary>A combo box entry: the text shown and the value it stands for.</summary>
internal sealed record ComboOption<T>(string Text, T Value)
{
    public override string ToString() => Text;
}

/// <summary>
/// Lets combo boxes work in terms of values instead of item positions, so reordering or adding
/// an entry can't silently change what a selection means.
/// </summary>
internal static class ComboBoxExtensions
{
    /// <summary>Replaces the items and selects the first one.</summary>
    public static void SetOptions<T>(this ComboBox comboBox, params ComboOption<T>[] options)
    {
        comboBox.Items.Clear();
        comboBox.Items.AddRange(options.Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;
    }

    public static T GetSelectedValue<T>(this ComboBox comboBox) =>
        ((ComboOption<T>)comboBox.SelectedItem!).Value;

    public static void SelectValue<T>(this ComboBox comboBox, T value)
    {
        var index = comboBox.Items.Cast<ComboOption<T>>().ToList()
            .FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, value));
        comboBox.SelectedIndex = index;
    }
}
