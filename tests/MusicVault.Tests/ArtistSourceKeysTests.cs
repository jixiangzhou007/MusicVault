using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class ArtistSourceKeysTests
{
    [Theory]
    [InlineData("Artist.Source.NetEaseMusic", "网易云音乐", "NetEase Music")]
    [InlineData("Artist.Source.QQMusic", "QQ音乐", "QQ Music")]
    public void Matches_legacy_and_encoded_source_names(string key, string zh, string en)
    {
        var stored = ServiceMessageCodec.Key(key);
        Assert.True(key switch
        {
            "Artist.Source.NetEaseMusic" => ArtistSourceKeys.IsNetEaseMusic(stored)
                && ArtistSourceKeys.IsNetEaseMusic(zh)
                && ArtistSourceKeys.IsNetEaseMusic(en),
            "Artist.Source.QQMusic" => ArtistSourceKeys.IsQQMusic(stored)
                && ArtistSourceKeys.IsQQMusic(zh)
                && ArtistSourceKeys.IsQQMusic(en),
            _ => false
        });
    }
}
