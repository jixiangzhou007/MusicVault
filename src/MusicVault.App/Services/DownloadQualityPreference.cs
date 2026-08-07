using MusicVault.Core.Enums;
using MusicVault.Services;

namespace MusicVault.App.Services;

/// <summary>发现页/偏好默认下载音质下拉：0=全部音质，1=128，2=320，3=FLAC。</summary>
public static class DownloadQualityPreference
{
    public const int AllQualitiesIndex = 0;

    public static int NormalizeIndex(int index) => Math.Clamp(index, 0, 3);

    /// <summary>旧版 2=FLAC（曾为默认值）。</summary>
    public const int LegacyFlacIndex = 2;

    /// <summary>旧版 +1 迁移误将 2 变为 3 时的错误结果。</summary>
    public const int MisMigratedLegacyFlacIndex = 3;

    /// <summary>旧版设置 0=128、1=320、2=FLAC（默认）；迁移为新版索引。0 不 +1（与新版「全部音质」冲突）。</summary>
    public static int MigrateLegacyStoredIndex(int stored) => stored switch
    {
        1 => 2,
        LegacyFlacIndex => AllQualitiesIndex,
        _ => AllQualitiesIndex
    };

    /// <summary>是否应对存储索引执行旧版迁移（0 与新版「全部音质」同为 0，不迁移）。</summary>
    public static bool ShouldMigrateStoredIndex(int stored) => stored is > 0 and <= LegacyFlacIndex;

    /// <summary>v1 误用 +1 迁移后，旧默认 FLAC 会存成 3；启动时纠正回「全部音质」（未手动改过时才纠正）。</summary>
    public static bool ShouldResetMisMigratedDefault(
        int stored, bool legacyMigrated, bool misMigrationCorrected, bool explicitlySet) =>
        !misMigrationCorrected && legacyMigrated && !explicitlySet && stored == MisMigratedLegacyFlacIndex;

    public static AudioQuality? ToFilterQuality(int index) => NormalizeIndex(index) switch
    {
        1 => AudioQuality.Standard128,
        2 => AudioQuality.High320,
        3 => AudioQuality.Flac,
        _ => null
    };

    public static string ComboLabel(int index) => NormalizeIndex(index) switch
    {
        1 => ServiceMessageCodec.Key("Download.Quality.Mp3128"),
        2 => ServiceMessageCodec.Key("Download.Quality.Mp3320"),
        3 => ServiceMessageCodec.Key("Download.Quality.Flac"),
        _ => ServiceMessageCodec.Key("Download.Quality.All")
    };
}
