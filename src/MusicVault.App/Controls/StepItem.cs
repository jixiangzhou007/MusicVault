namespace MusicVault.App.Controls;

public sealed class StepItem
{
    public StepItem(string title, StepStatus status, int stepNumber, bool showConnector = true)
    {
        Title = title;
        Status = status;
        StepNumber = stepNumber;
        ShowConnector = showConnector;
    }

    public string Title { get; }

    public StepStatus Status { get; }

    public int StepNumber { get; }

    public bool ShowConnector { get; }
}
