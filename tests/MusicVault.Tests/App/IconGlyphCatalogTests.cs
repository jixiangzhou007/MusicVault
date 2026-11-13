using System.Reflection;
using System.Text.RegularExpressions;
using MusicVault.App.Icons;

namespace MusicVault.Tests.App;

public sealed class IconGlyphCatalogTests
{
    private static readonly Regex AxamlGlyph = new(
        @"x:Key=""IconGlyph(\w+)"">&#x([0-9A-Fa-f]+);",
        RegexOptions.Compiled);

    private static readonly Regex HardcodedEntity = new(
        @"&#x[Ee][0-9A-Fa-f]{3};",
        RegexOptions.Compiled);

    [Fact]
    public void Heart_uses_current_fluent_not_legacy_broken()
    {
        Assert.Equal("\uEB51", IconGlyphs.Heart);
        Assert.Equal("\uEB52", IconGlyphs.HeartFill);
        Assert.NotEqual("\uE006", IconGlyphs.Heart);
        Assert.NotEqual("\uE007", IconGlyphs.HeartFill);
    }

    [Fact]
    public void Semantic_glyphs_are_not_reused_across_unrelated_actions()
    {
        Assert.NotEqual(IconGlyphs.Theme, IconGlyphs.Equalizer);
        Assert.NotEqual(IconGlyphs.Lyrics, "\uE8AB");
        Assert.NotEqual(IconGlyphs.Queue, IconGlyphs.Sort);
        Assert.NotEqual(IconGlyphs.CompactPlayer, IconGlyphs.Restore);
        Assert.NotEqual(IconGlyphs.Playlist, "\uE8DD");
    }

    [Fact]
    public void Icons_axaml_matches_icon_glyphs_class()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "MusicVault.App", "Styles", "Tokens", "Icons.axaml"));
        var fromAxaml = AxamlGlyph.Matches(axaml)
            .Cast<Match>()
            .ToDictionary(m => m.Groups[1].Value, m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[2].Value, 16)));

        var fromCs = typeof(IconGlyphs)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

        Assert.Equal(fromCs.Keys.OrderBy(k => k), fromAxaml.Keys.OrderBy(k => k));
        foreach (var (name, glyph) in fromCs)
            Assert.Equal(glyph, fromAxaml[name]);
    }

    [Fact]
    public void Views_and_styles_do_not_hardcode_fluent_codepoints()
    {
        var appRoot = Path.Combine(FindRepoRoot(), "src", "MusicVault.App");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(appRoot, "Styles", "Tokens", "Icons.axaml"),
        };

        var violations = Directory.EnumerateFiles(appRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(path => !allowed.Contains(path))
            .SelectMany(path => HardcodedEntity.Matches(File.ReadAllText(path))
                .Select(m => $"{Path.GetRelativePath(appRoot, path)}:{m.Value}"))
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MusicVault.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("MusicVault.sln not found from test output.");
    }
}
