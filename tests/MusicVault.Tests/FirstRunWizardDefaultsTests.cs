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

    [Fact]
    public void AppSettings_onboarding_path_defaults_unset()
    {
        var settings = new AppSettings();
        Assert.Equal(FirstRunOnboardingPath.Unset, settings.FirstRunOnboardingPath);
    }

    [Fact]
    public void Missing_onboarding_path_json_field_deserializes_to_unset()
    {
        var json = """{"FirstRunWizardCompleted":true}""";
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(settings);
        Assert.True(settings.FirstRunWizardCompleted);
        Assert.Equal(FirstRunOnboardingPath.Unset, settings.FirstRunOnboardingPath);
    }
}
