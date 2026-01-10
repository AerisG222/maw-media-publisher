namespace MawMediaPublisher.Scale;

public record ScaledFile(
    Guid Id,
    ScaleSpec Scale,
    string Path,
    int Width,
    int Height,
    long Bytes
);
