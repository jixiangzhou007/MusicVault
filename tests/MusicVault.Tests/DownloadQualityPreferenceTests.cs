using MusicVault.App.Services;
using MusicVault.Core.Enums;

namespace MusicVault.Tests;

public sealed class DownloadQualityPreferenceTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, AudioQuality.Standard128)]
    [InlineData(2, AudioQuality.High320)]
    [InlineData(3, AudioQuality.Flac)]
    public void ToFilterQuality_MapsIndex(int index, AudioQuality? expected) =>
        Assert.Equal(expected, DownloadQualityPreference.ToFilterQuality(index));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    public void NormalizeIndex_Clamps(int input, int expected) =>
        Assert.Equal(expected, DownloadQualityPreference.NormalizeIndex(input));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    public void MigrateLegacyStoredIndex_MapsOldValues(int legacy, int expected) =>
        Assert.Equal(expected, DownloadQualityPreference.MigrateLegacyStoredIndex(legacy));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void ShouldMigrateStoredIndex_OnlyLegacy320AndFlac(int stored, bool expected) =>
        Assert.Equal(expected, DownloadQualityPreference.ShouldMigrateStoredIndex(stored));

    [Theory]
    [InlineData(3, true, false, false, true)]
    [InlineData(3, true, false, true, false)]
    [InlineData(3, true, true, false, false)]
    [InlineData(3, false, false, false, false)]
    [InlineData(2, true, false, false, false)]
    public void ShouldResetMisMigratedDefault_OnlyV1FlacBug(
        int stored, bool legacyMigrated, bool misMigrationCorrected, bool explicitlySet, bool expected) =>
        Assert.Equal(
            expected,
            DownloadQualityPreference.ShouldResetMisMigratedDefault(
                stored, legacyMigrated, misMigrationCorrected, explicitlySet));
}
