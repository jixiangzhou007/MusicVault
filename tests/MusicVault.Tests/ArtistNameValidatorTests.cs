using MusicVault.Core.Artists;

using MusicVault.Services.Metadata;



namespace MusicVault.Tests;



public sealed class ArtistNameValidatorTests

{

    [Fact]

    public async Task ValidateArtistNameAsync_ReturnsRecognized_WhenNetEaseMatches()

    {

        var validator = new ArtistNameValidator(

            neteaseSearch: (_, _) => Task.FromResult<bool?>(true),

            qqSearch: (_, _) => Task.FromResult<bool?>(false));



        Assert.Equal(

            ArtistNameValidationStatus.Recognized,

            await validator.ValidateArtistNameAsync("周杰伦", CancellationToken.None));

    }



    [Fact]

    public async Task ValidateArtistNameAsync_ReturnsRecognized_WhenQqMatches()

    {

        var validator = new ArtistNameValidator(

            neteaseSearch: (_, _) => Task.FromResult<bool?>(false),

            qqSearch: (_, _) => Task.FromResult<bool?>(true));



        Assert.Equal(

            ArtistNameValidationStatus.Recognized,

            await validator.ValidateArtistNameAsync("张学友", CancellationToken.None));

    }



    [Fact]

    public async Task ValidateArtistNameAsync_NormalizesTrailingPunctuation()

    {

        var seen = new List<string>();

        var validator = new ArtistNameValidator(

            neteaseSearch: (name, _) =>

            {

                seen.Add(name);

                return Task.FromResult<bool?>(name == "周杰伦");

            },

            qqSearch: (_, _) => Task.FromResult<bool?>(false));



        Assert.Equal(

            ArtistNameValidationStatus.Recognized,

            await validator.ValidateArtistNameAsync("周杰伦.", CancellationToken.None));

        Assert.Contains("周杰伦", seen);

    }



    [Fact]

    public async Task ValidateArtistNameAsync_ReturnsNotRecognized_ForLabelLikeName()

    {

        var validator = new ArtistNameValidator(

            neteaseSearch: (_, _) => Task.FromResult<bool?>(false),

            qqSearch: (_, _) => Task.FromResult<bool?>(false));



        Assert.Equal(

            ArtistNameValidationStatus.NotRecognized,

            await validator.ValidateArtistNameAsync("索尼音乐", CancellationToken.None));

    }



    [Fact]

    public async Task ValidateArtistNameAsync_ReturnsInconclusive_WhenOnlyNetEaseRejects()

    {

        var validator = new ArtistNameValidator(

            neteaseSearch: (_, _) => Task.FromResult<bool?>(false),

            qqSearch: (_, _) => Task.FromResult<bool?>(null));



        Assert.Equal(

            ArtistNameValidationStatus.Inconclusive,

            await validator.ValidateArtistNameAsync("本地独立乐队", CancellationToken.None));

    }



    [Fact]

    public async Task FilterArtistFieldAsync_KeepsOnlyRecognizedSegments()

    {

        var recognized = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "周杰伦" };

        var validator = new ArtistNameValidator(

            neteaseSearch: (name, _) => Task.FromResult<bool?>(recognized.Contains(name)),

            qqSearch: (_, _) => Task.FromResult<bool?>(false));



        var result = await validator.FilterArtistFieldAsync("周杰伦 / 索尼音乐", CancellationToken.None);

        Assert.Equal("周杰伦", result);

    }



    [Fact]

    public async Task FilterArtistFieldAsync_KeepsSegment_WhenValidationInconclusive()

    {

        var validator = new ArtistNameValidator(

            neteaseSearch: (_, _) => Task.FromResult<bool?>(null),

            qqSearch: (_, _) => Task.FromResult<bool?>(null));



        var result = await validator.FilterArtistFieldAsync("本地独立乐队", CancellationToken.None);

        Assert.Equal("本地独立乐队", result);

    }



    [Fact]

    public async Task FilterArtistFieldAsync_ValidatesFeatSegments()

    {

        var recognized = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "周杰伦", "蔡依林" };

        var validator = new ArtistNameValidator(

            neteaseSearch: (name, _) => Task.FromResult<bool?>(recognized.Contains(name)),

            qqSearch: (_, _) => Task.FromResult<bool?>(false));



        var result = await validator.FilterArtistFieldAsync("周杰伦 feat. 蔡依林", CancellationToken.None);

        Assert.Equal("周杰伦 / 蔡依林", result);

    }

}


