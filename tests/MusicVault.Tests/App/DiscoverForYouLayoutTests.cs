using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverForYouLayoutTests
{
    [Theory]
    [InlineData(500, 2)]
    [InlineData(799, 2)]
    [InlineData(800, 3)]
    [InlineData(1200, 3)]
    public void Columns_from_width(double width, int expected) =>
        Assert.Equal(expected, DiscoverForYouLayout.ColumnsForWidth(width));

    [Theory]
    [InlineData(2, 10)] // 2×5
    [InlineData(3, 12)] // 3×4
    public void PageSize_from_columns(int columns, int expected) =>
        Assert.Equal(expected, DiscoverForYouLayout.PageSizeForColumns(columns));
}
