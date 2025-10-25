namespace MawMediaPublisher.Models;

public static class RecognizedExtensions
{
    public static readonly string[] RawFileExtensions = [
        ".DNG",
        ".NEF",
    ];

    public static readonly string[] SourceImageExtensions = [
        ".AVIF",
        ".HEIC",
        ".JPG",
        ".NEF",
        ".PNG"
    ];

    public static readonly string[] SourceVideoExtensions = [
        ".3GP",
        ".AVI",
        ".FLV",
        ".M4V",
        ".MKV",
        ".MOV",
        ".MP4",
        ".MPEG",
        ".MPG",
        ".VOB"
    ];

    const string EXT_DNG = ".DNG";
    const string EXT_PP3 = ".PP3";

    public static readonly string[] SupportExtensions = [
        EXT_DNG,
        EXT_PP3
    ];

    public static bool IsRaw(string file) =>
        RawFileExtensions.Contains(Path.GetExtension(file)?.ToUpper());

    public static bool IsDng(string file) =>
        string.Equals(EXT_DNG, Path.GetExtension(file)?.ToUpper());

    public static bool IsPp3(string file) =>
        string.Equals(EXT_PP3, Path.GetExtension(file)?.ToUpper());

    public static bool IsSourceImage(string file) =>
        SourceImageExtensions.Contains(Path.GetExtension(file)?.ToUpper());

    public static bool IsSourceVideo(string file) =>
        SourceVideoExtensions.Contains(Path.GetExtension(file)?.ToUpper());

    public static bool IsSupportFile(string file) =>
        SupportExtensions.Contains(Path.GetExtension(file)?.ToUpper());
}
