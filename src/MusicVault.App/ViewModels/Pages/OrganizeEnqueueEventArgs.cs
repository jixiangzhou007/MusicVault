using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>从曲库等页面请求将曲目加入整理工作清单。</summary>
public sealed class OrganizeEnqueueEventArgs(
    IReadOnlyList<Track> tracks,
    string sourceLabel,
    bool append,
    int organizeCategoryIndex) : EventArgs
{
    public IReadOnlyList<Track> Tracks { get; } = tracks;
    public string SourceLabel { get; } = sourceLabel;
    public bool Append { get; } = append;

    /// <summary>进入整理页后选中的 Tab 索引。</summary>
    public int OrganizeCategoryIndex { get; } = organizeCategoryIndex;
}
