namespace MusicVault.Services.Http;

/// <summary>共享 HttpClient 实例，避免套接字耗尽，降低内存占用</summary>
public static class HttpClients
{
    public static HttpClient Default { get; } = CreateClient();

    public static HttpClient Download { get; } = CreateClient(TimeSpan.FromMinutes(30));

    private static HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 8,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add("User-Agent",
            "MusicVault/1.0 (Personal Music Manager)");
        return client;
    }
}
