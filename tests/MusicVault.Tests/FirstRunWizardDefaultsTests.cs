using MusicVault.App.Services;

namespace MusicVault.Tests;

public sealed class FirstRunWizardDefaultsTests
{
    [Fact]
    public void AppSettings_first_run_flag_defaults_false()
    {
        var settings = new AppSettings();
        Assert.False(settings.FirstRunWizardCompleted);
    }
}
