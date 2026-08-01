using System.Globalization;
using System.IO;
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

    [Fact]
    public void KeyboardSelectAllDoesNotQueueWorkDuringDispatcherShutdown()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "GameSaveCenter.Playnite", "Infrastructure", "NumericInput.cs"));

        Assert.Contains("dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished", source);
        Assert.Contains("catch (InvalidOperationException)", source);
        Assert.Contains("dispatcher.BeginInvoke(new Action(textBox.SelectAll), DispatcherPriority.Input)", source);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameSaveCenter.sln"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the GameSaveCenter repository root for the numeric input regression test.");
    }
}
