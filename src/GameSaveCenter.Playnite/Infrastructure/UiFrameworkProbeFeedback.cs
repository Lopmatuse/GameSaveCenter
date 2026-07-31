using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GameSaveCenter.Playnite.Infrastructure;

/// <summary>Contains failures from the development-only WPF-UI probe at its UI event boundary.</summary>
public sealed class UiFrameworkProbeFeedback
{
    private readonly Action<Exception> logFailure;
    private readonly Action<string> reportFailure;

    public UiFrameworkProbeFeedback(Action<Exception> logFailure, Action<string> reportFailure)
    {
        this.logFailure = logFailure ?? throw new ArgumentNullException(nameof(logFailure));
        this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
    }

    public async Task<bool> TryShowAsync(Func<Task> show)
    {
        if (show == null)
        {
            throw new ArgumentNullException(nameof(show));
        }

        try
        {
            await show();
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                logFailure(exception);
            }
            catch (Exception loggingException)
            {
                Trace.TraceError("GameSaveCenter WPF-UI probe logging failed: {0}", loggingException);
            }

            try
            {
                reportFailure($"无法显示 WPF-UI 浮层：{exception.Message}");
            }
            catch (Exception reportingException)
            {
                Trace.TraceError("GameSaveCenter WPF-UI probe failure reporting failed: {0}", reportingException);
            }

            return false;
        }
    }
}
