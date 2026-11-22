using MawMediaPublisher.Deploy;

namespace MawMediaPublisher.Tests;

public class ShellQuoteTests
{
    [Theory]
    [InlineData("/home/svc/media-assets/2026", "'/home/svc/media-assets/2026'")]
    [InlineData("/assets/2026/beach day", "'/assets/2026/beach day'")]
    [InlineData("/assets/2026/mike's party", "'/assets/2026/mike'\\''s party'")]
    public void ShellQuote_WrapsAndEscapes(string value, string expected)
    {
        Assert.Equal(expected, ProductionDeployer.ShellQuote(value));
    }
}
