namespace MusicVault.Tests.App;

/// <summary>ant-switch 合同：PART_MovingKnobs 必须在 Canvas(PART_SwitchKnob) 内，否则 checked 时圆钮不移动。</summary>
public sealed class MusicVaultSwitchThemeTests
{
    [Fact]
    public void Switch_theme_uses_canvas_travel_for_moving_knobs()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "MusicVault.App", "Styles", "MetadataControlThemes.axaml");
        var axaml = File.ReadAllText(path);
        Assert.Contains("x:Key=\"MusicVaultSwitchTheme\"", axaml);
        Assert.Contains("x:Name=\"PART_SwitchKnob\"", axaml);
        Assert.Contains("x:Name=\"PART_MovingKnobs\"", axaml);
        Assert.Contains("<Canvas x:Name=\"PART_SwitchKnob\"", axaml);
        Assert.DoesNotContain("<Panel x:Name=\"PART_SwitchKnob\"", axaml);
        Assert.Contains("ClipToBounds=\"False\"", axaml);
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
