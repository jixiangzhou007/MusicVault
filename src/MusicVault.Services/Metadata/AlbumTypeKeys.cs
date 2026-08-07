using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>在线专辑类型 i18n key。</summary>
public static class AlbumTypeKeys
{
    public static string Album => ServiceMessageCodec.Key("Album.Type.Album");
    public static string Single => ServiceMessageCodec.Key("Album.Type.Single");

    public static string? FromNetEaseTypeCode(int code) => code switch
    {
        0 => Album,
        1 => Single,
        2 or 3 => "EP",
        _ => null
    };
}
