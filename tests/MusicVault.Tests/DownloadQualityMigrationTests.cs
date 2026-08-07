using MusicVault.App.Services;

namespace MusicVault.Tests;

public sealed class DownloadQualityMigrationTests
{
    [Fact]
    public void ApplyDownloadQualityMigration_LegacyFlacDefault_BecomesAllQualities()
    {
        var settings = new AppSettings { DefaultDownloadQualityIndex = 2 };

        Assert.True(MusicApiBootstrap.ApplyDownloadQualityMigration(settings));
        Assert.Equal(DownloadQualityPreference.AllQualitiesIndex, settings.DefaultDownloadQualityIndex);
        Assert.True(settings.DefaultDownloadQualityMigrated);
    }

    [Fact]
    public void ApplyDownloadQualityMigration_DoesNotShiftZeroToOne()
    {
        var settings = new AppSettings { DefaultDownloadQualityIndex = 0 };

        Assert.True(MusicApiBootstrap.ApplyDownloadQualityMigration(settings));
        Assert.Equal(0, settings.DefaultDownloadQualityIndex);
    }

    [Fact]
    public void ApplyDownloadQualityMigration_ResetsMisMigratedFlacDefault()
    {
        var settings = new AppSettings
        {
            DefaultDownloadQualityIndex = 3,
            DefaultDownloadQualityMigrated = true
        };

        Assert.True(MusicApiBootstrap.ApplyDownloadQualityMigration(settings));
        Assert.Equal(DownloadQualityPreference.AllQualitiesIndex, settings.DefaultDownloadQualityIndex);
        Assert.True(settings.DefaultDownloadQualityMisMigrationCorrected);
    }

    [Fact]
    public void ApplyDownloadQualityMigration_KeepsExplicitFlacChoice()
    {
        var settings = new AppSettings
        {
            DefaultDownloadQualityIndex = 3,
            DefaultDownloadQualityMigrated = true,
            DefaultDownloadQualityExplicitlySet = true
        };

        Assert.True(MusicApiBootstrap.ApplyDownloadQualityMigration(settings));
        Assert.Equal(3, settings.DefaultDownloadQualityIndex);
        Assert.True(settings.DefaultDownloadQualityMisMigrationCorrected);
    }
}
