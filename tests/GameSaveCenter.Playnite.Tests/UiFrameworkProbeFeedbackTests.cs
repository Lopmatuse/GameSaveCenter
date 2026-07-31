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

    [Fact]
    public void LazyProbeConstructionFailureLeavesARecoverableFailureMessage()
    {
        Exception? logged = null;
        var loader = new UiFrameworkProbeLoader(exception => logged = exception);

        var result = loader.TryCreate<object>(
            () => throw new InvalidOperationException("WPF-UI resource unavailable"),
            out var probe,
            out var failure);

        Assert.False(result);
        Assert.Null(probe);
        Assert.IsType<InvalidOperationException>(logged);
        Assert.Contains("WPF-UI resource unavailable", failure);
        Assert.Contains("维护中心仍可继续使用", failure);
    }

    [Fact]
    public void LazyProbeConstructionReturnsTheOptInControlOnlyAfterSuccessfulCreation()
    {
        var loader = new UiFrameworkProbeLoader(_ => { });

        var result = loader.TryCreate(() => new object(), out var probe, out var failure);

        Assert.True(result);
        Assert.NotNull(probe);
        Assert.Equal(string.Empty, failure);
    }
}
