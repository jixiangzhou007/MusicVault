namespace MusicVault.App.Services;

public static class DiscoverForYouLayout
{
    public const double ThreeColumnMinWidth = 800;

    public static int ColumnsForWidth(double width) =>
        width >= ThreeColumnMinWidth ? 3 : 2;

    public static int PageSizeForColumns(int columns) =>
        columns >= 3 ? 12 : 10;

    public static int PageSizeForWidth(double width) =>
        PageSizeForColumns(ColumnsForWidth(width));
}
