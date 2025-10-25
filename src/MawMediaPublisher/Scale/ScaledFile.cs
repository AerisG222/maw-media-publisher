namespace MawMediaPublisher.Scale;

public record class ScaledFile(
    ScaleSpec Scale,
    string Path,
    int Width,
    int Height,
    long Bytes
);
