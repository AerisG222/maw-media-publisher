using MawMediaPublisher.Models;

namespace MawMediaPublisher.Tests;

public class SlugHelperTests
{
    [Theory]
    // existing behavior that must not change
    [InlineData("IMG_1234", "img-1234")]
    [InlineData("PXL_20260814_165036281", "pxl-20260814-165036281")]
    [InlineData("9/11 Memorial", "9-11-memorial")]
    [InlineData("Beach - Day 2", "beach-day-2")]
    [InlineData("test category's name", "test-categorys-name")]
    [InlineData(@"a\b", "a-b")]
    // removed characters next to separators should not leave double dashes
    [InlineData("Tom & Jerry", "tom-jerry")]
    [InlineData("Lunch @ Joe's (Downtown)", "lunch-joes-downtown")]
    // no leading / trailing dashes
    [InlineData(" Trailing (2)", "trailing-2")]
    [InlineData("_leading", "leading")]
    [InlineData("trailing!", "trailing")]
    public void MakeSafeSlug_ProducesCleanSlugs(string value, string expected)
    {
        Assert.Equal(expected, SlugHelper.MakeSafeSlug(value));
    }
}
