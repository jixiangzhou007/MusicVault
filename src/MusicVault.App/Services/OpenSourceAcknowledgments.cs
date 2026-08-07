namespace MusicVault.App.Services;

/// <summary>偏好页开源致谢条目 key（Summary 悬停，Detail 弹窗）。</summary>
public sealed record OpenSourceProjectKeys(string Id, string NameKey, string SummaryKey, string DetailKey);

/// <summary>偏好页「介绍」开源致谢列表 key。</summary>
public static class OpenSourceAcknowledgments
{
    public static IReadOnlyList<OpenSourceProjectKeys> ProjectKeys { get; } =
    [
        new("avalonia", "Settings.OpenSource.Avalonia.Name", "Settings.OpenSource.Avalonia.Summary", "Settings.OpenSource.Avalonia.Detail"),
        new("dotnet", "Settings.OpenSource.DotNet.Name", "Settings.OpenSource.DotNet.Summary", "Settings.OpenSource.DotNet.Detail"),
        new("mvvm", "Settings.OpenSource.Mvvm.Name", "Settings.OpenSource.Mvvm.Summary", "Settings.OpenSource.Mvvm.Detail"),
        new("velopack", "Settings.OpenSource.Velopack.Name", "Settings.OpenSource.Velopack.Summary", "Settings.OpenSource.Velopack.Detail"),
        new("sqlite", "Settings.OpenSource.Sqlite.Name", "Settings.OpenSource.Sqlite.Summary", "Settings.OpenSource.Sqlite.Detail"),
        new("taglib", "Settings.OpenSource.TagLib.Name", "Settings.OpenSource.TagLib.Summary", "Settings.OpenSource.TagLib.Detail"),
        new("naudio", "Settings.OpenSource.NAudio.Name", "Settings.OpenSource.NAudio.Summary", "Settings.OpenSource.NAudio.Detail"),
        new("libvlc", "Settings.OpenSource.LibVlc.Name", "Settings.OpenSource.LibVlc.Summary", "Settings.OpenSource.LibVlc.Detail"),
        new("pinyin", "Settings.OpenSource.PinYin.Name", "Settings.OpenSource.PinYin.Summary", "Settings.OpenSource.PinYin.Detail"),
        new("gomusicapi", "Settings.OpenSource.GoMusicApi.Name", "Settings.OpenSource.GoMusicApi.Summary", "Settings.OpenSource.GoMusicApi.Detail"),
        new("neteaseapi", "Settings.OpenSource.NetEaseApi.Name", "Settings.OpenSource.NetEaseApi.Summary", "Settings.OpenSource.NetEaseApi.Detail"),
    ];
}

/// <summary>UI 绑定的已解析开源条目。</summary>
public sealed record OpenSourceProject(string Id, string Name, string Summary, string Detail);
