using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Tests;

public class StudentIdentityTests
{
    [Theory]
    [InlineData("20152691234567890", "20152691234567890")]
    [InlineData("  20152691234567890 ", "20152691234567890")]
    [InlineData("00000000000000001", "00000000000000001")] // leading zeros are kept
    public void TryNormalize_AcceptsSeventeenDigits(string raw, string expected)
    {
        Assert.True(StudentIdentity.TryNormalize(raw, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2015269123456789")]   // 16 digits
    [InlineData("201526912345678901")] // 18 digits
    [InlineData("2015269123456789A")]
    [InlineData("20152691 34567890")]
    [InlineData("-2015269123456789")]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]  // Bangla digits: the UI converts them before sending
    public void TryNormalize_RejectsEverythingElse(string? raw)
    {
        Assert.False(StudentIdentity.TryNormalize(raw, out _));
    }
}
