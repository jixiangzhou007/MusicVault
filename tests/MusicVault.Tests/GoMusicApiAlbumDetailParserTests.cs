using System.Text.Json;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiAlbumDetailParserTests
{
    [Fact]
    public void ParseAlbumDetailTracks_parses_flat_song_array()
    {
        var samplePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "album-detail-sample.json"));

        var json = File.Exists(samplePath)
            ? File.ReadAllText(samplePath)
            : """
              {"code":200,"msg":"success","data":[
                {"id":"2022072825","name":"又到天黑","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022069283","name":"我无法克制自己","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022072826","name":"我想要占据你","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022069284","name":"一念之间","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022069285","name":"你所到之处 (就下雨)","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022069286","name":"啊我忘了带伞","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022069287","name":"你看你看看","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022072827","name":"远距离恋爱","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022072828","name":"带你飞","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"},
                {"id":"2022072829","name":"爱你我没问题","artist":"告五人","album":"带你飞","album_id":"159967997","source":"netease"}
              ]}
              """;

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var tracks = GoMusicApiClient.ParseAlbumDetailTracks(data, "告五人", "带你飞");

        Assert.Equal(10, tracks.Count);
        Assert.Equal("又到天黑", tracks[0].Title);
        Assert.Equal("爱你我没问题", tracks[9].Title);
        Assert.Equal("159967997", tracks[0].AlbumId);
        Assert.Equal(1, tracks[0].TrackNumber);
        Assert.Equal(10, tracks[9].TrackNumber);
    }
}
