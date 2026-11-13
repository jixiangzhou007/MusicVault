namespace MusicVault.App.Services;

public sealed record SoftwareFeatureCardKeys(string TitleKey, string DescriptionKey);

/// <summary>偏好页「软件介绍」文案 key（实际文本在 LocalizationResources）。</summary>
public static class SoftwareGuideCatalog
{
    public const string TaglineKey = "Settings.Guide.Tagline";
    public const string AuthorKey = "Settings.Guide.Author";
    public const string HeroLeadKey = "Settings.Guide.HeroLead";
    public const string HeroDisclaimerKey = "Settings.Guide.HeroDisclaimer";
    public const string CapabilitiesSummaryKey = "Settings.Guide.CapabilitiesSummary";
    public const string CopyrightKey = "Settings.Guide.Copyright";
    public const string OpenSourceSectionTitleKey = "Settings.Guide.OpenSourceTitle";

    public static IReadOnlyList<SoftwareFeatureCardKeys> FeatureCardKeys { get; } =
    [
        new("Settings.Guide.Feature.Library.Title", "Settings.Guide.Feature.Library.Desc"),
        new("Settings.Guide.Feature.Discover.Title", "Settings.Guide.Feature.Discover.Desc"),
        new("Settings.Guide.Feature.Download.Title", "Settings.Guide.Feature.Download.Desc"),
        new("Settings.Guide.Feature.Playback.Title", "Settings.Guide.Feature.Playback.Desc"),
        new("Settings.Guide.Feature.Metadata.Title", "Settings.Guide.Feature.Metadata.Desc"),
        new("Settings.Guide.Feature.Settings.Title", "Settings.Guide.Feature.Settings.Desc"),
    ];
}
