namespace MawMediaPublisher.Scale;

public record class ScaleSpec(
    string Code,
    int Width,
    int Height,
    bool IsCropToFill,
    bool IsPoster
)
{
    static readonly ScaleSpec[] _allScales = [
        new ("qqvg",             160,  120, false, false),
        new ("qqvg",             160,  120, false, true),
        new ("qqvg-fill",        160,  120, true,  false),
        new ("qqvg-fill",        160,  120, true,  true),
        new ("qvg",              320,  240, false, false),
        new ("qvg",              320,  240, false, true),
        new ("qvg-fill",         320,  240, true,  false),
        new ("qvg-fill",         320,  240, true,  true),
        new ("nhd",              640,  480, false, false),
        new ("nhd",              640,  480, false, true),
        new ("full-hd",         1920, 1080, false, false),
        new ("full-hd",         1920, 1080, false, true),
        new ("qhd",             2560, 1440, false, false),
        new ("qhd",             2560, 1440, false, true),
        new ("4k",              3840, 2160, false, false),
        new ("4k",              3840, 2160, false, true),
        new ("5k",              5120, 2880, false, false),
        new ("5k",              5120, 2880, false, true),
        new ("full",            int.MaxValue, int.MaxValue, false, false),
        new ("full",            int.MaxValue, int.MaxValue, false, true)
    ];

    public static ScaleSpec[] AllScales => _allScales;
}
