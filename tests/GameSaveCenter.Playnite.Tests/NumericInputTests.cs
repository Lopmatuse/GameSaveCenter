using System.Globalization;
using GameSaveCenter.Playnite.Infrastructure;
using Xunit;

namespace GameSaveCenter.Playnite.Tests;

public sealed class NumericInputTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("1440", true)]
    [InlineData("0", false)]
    [InlineData("1441", false)]
    [InlineData("12x", false)]
    public void IntegerRangeValidationRule_ValidatesCompleteMinuteValues(string text, bool expectedValid)
    {
        var rule = new IntegerRangeValidationRule { Minimum = 1, Maximum = 1440 };
        var result = rule.Validate(text, CultureInfo.InvariantCulture);
        Assert.Equal(expectedValid, result.IsValid);
    }
}
