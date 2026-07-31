using System;
using System.Threading.Tasks;
using GameSaveCenter.Playnite.Infrastructure;
using Xunit;

namespace GameSaveCenter.Playnite.Tests;

public sealed class UiFrameworkProbeFeedbackTests
{
    [Fact]
    public async Task DialogFailureIsLoggedAndReportedWithoutEscapingTheUiBoundary()
    {
        Exception? logged = null;
        string? reported = null;
        var feedback = new UiFrameworkProbeFeedback(exception => logged = exception, message => reported = message);

        var result = await feedback.TryShowAsync(() => Task.FromException(new InvalidOperationException("host unavailable")));

        Assert.False(result);
        var exception = Assert.IsType<InvalidOperationException>(logged);
        Assert.Equal("host unavailable", exception.Message);
        Assert.Contains("host unavailable", reported);
    }

    [Fact]
    public async Task SynchronousConstructionFailureIsContainedByTheSameUiBoundary()
    {
        Exception? logged = null;
        string? reported = null;
        var feedback = new UiFrameworkProbeFeedback(exception => logged = exception, message => reported = message);

        var result = await feedback.TryShowAsync(() => throw new InvalidOperationException("dialog host unavailable"));

        Assert.False(result);
        Assert.IsType<InvalidOperationException>(logged);
        Assert.Contains("dialog host unavailable", reported);
    }

    [Fact]
    public async Task FailureCallbacksCannotReintroduceAnUnhandledUiException()
    {
        var feedback = new UiFrameworkProbeFeedback(
            _ => throw new InvalidOperationException("logger unavailable"),
            _ => throw new InvalidOperationException("feedback unavailable"));

        var result = await feedback.TryShowAsync(() => Task.FromException(new InvalidOperationException("dialog failed")));

        Assert.False(result);
    }
}
