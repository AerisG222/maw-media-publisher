namespace MawMediaPublisher.Scale;

public record class ScaledFile(
    Guid Id,
    ScaleSpec Scale,
    string Path,
    int Width,
    int Height,
    long Bytes
);
