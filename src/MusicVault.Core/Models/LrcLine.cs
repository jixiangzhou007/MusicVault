namespace MusicVault.Core.Models;

/// <summary>LRC 歌词单行</summary>
public sealed class LrcLine
{
    public double TimeSeconds { get; init; }
    public string Text { get; init; } = string.Empty;
    /// <summary>中文对照（非中文歌曲时显示在原文下方）。</summary>
    public string? TranslationText { get; init; }
    public bool HasTranslation => !string.IsNullOrWhiteSpace(TranslationText);
    public bool IsActive { get; set; }
}
