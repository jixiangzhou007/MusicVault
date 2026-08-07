using MusicVault.Services.Library;

namespace MusicVault.Tests;

public sealed class WebDavClientTests
{
    [Fact]
    public void ParsePropfindResponse_ExtractsFilesAndFolders()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <D:multistatus xmlns:D="DAV:">
              <D:response>
                <D:href>/music/</D:href>
                <D:propstat>
                  <D:prop>
                    <D:resourcetype><D:collection/></D:resourcetype>
                  </D:prop>
                  <D:status>HTTP/1.1 200 OK</D:status>
                </D:propstat>
              </D:response>
              <D:response>
                <D:href>/music/song.mp3</D:href>
                <D:propstat>
                  <D:prop>
                    <D:resourcetype/>
                    <D:getcontentlength>12345</D:getcontentlength>
                  </D:prop>
                  <D:status>HTTP/1.1 200 OK</D:status>
                </D:propstat>
              </D:response>
            </D:multistatus>
            """;

        var entries = WebDavClient.ParsePropfindResponse("https://example.com/music/", xml);

        Assert.Single(entries);
        var file = entries[0];
        Assert.False(file.IsCollection);
        Assert.EndsWith("song.mp3", file.Url);
        Assert.Equal(12345, file.SizeBytes);
    }

    [Fact]
    public void ParsePropfindResponse_SkipsSelfFolderEntry()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <D:multistatus xmlns:D="DAV:">
              <D:response>
                <D:href>/music/</D:href>
                <D:propstat>
                  <D:prop><D:resourcetype><D:collection/></D:resourcetype></D:prop>
                  <D:status>HTTP/1.1 200 OK</D:status>
                </D:propstat>
              </D:response>
            </D:multistatus>
            """;

        var entries = WebDavClient.ParsePropfindResponse("https://example.com/music/", xml);

        Assert.Empty(entries);
    }
}
