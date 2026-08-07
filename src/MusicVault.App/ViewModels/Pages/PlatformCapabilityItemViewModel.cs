using MusicVault.App.Services;

namespace MusicVault.App.ViewModels.Pages;

public sealed class PlatformCapabilityItemViewModel
{
    public PlatformCapabilityItemViewModel(
        string name,
        string status,
        CapabilityStatusPresentation tag,
        string detail)
    {
        Name = name;
        Status = status;
        Tag = tag;
        Detail = detail;
    }

    public string Name { get; }

    public string Status { get; }

    public CapabilityStatusPresentation Tag { get; }

    public string Detail { get; }
}
