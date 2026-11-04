using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DaisiProvider.Desktop;

public sealed record HelpTopic(string Id, string Title)
{
    // Name shown to screen readers and UI Automation.
    public override string ToString() => Title;
}

public partial class HelpWindow : Window
{
    // Each topic is the embedded file Help\<Id>.md.
    private static readonly HelpTopic[] Topics =
    [
        new("overview", "Overview"),
        new("process", "Process step by step"),
        new("monitor", "Monitor"),
        new("manual", "Manual upload"),
        new("destination", "Destination and paths"),
        new("copy-rules", "Copy rules"),
        new("log-json", "The log JSON"),
        new("troubleshooting", "Troubleshooting")
    ];

    public HelpWindow()
    {
        InitializeComponent();
        TopicList.ItemsSource = Topics;
        TopicList.SelectedIndex = 0;
    }

    public void ShowTopic(string id) =>
        TopicList.SelectedItem = Topics.FirstOrDefault(topic => topic.Id == id) ?? Topics[0];

    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (TopicList.SelectedItem is HelpTopic topic)
        {
            TopicViewer.Document = HelpDocumentBuilder.Build(ReadTopic(topic));
        }
    }

    private static string ReadTopic(HelpTopic topic)
    {
        var resourceName = $"Help.{topic.Id}.md";
        using var stream = typeof(HelpWindow).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Help topic is missing from the build: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
