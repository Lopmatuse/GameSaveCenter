using System;
using System.Diagnostics;

namespace GameSaveCenter.Playnite.Infrastructure;

/// <summary>
/// Keeps the optional WPF-UI probe outside Dashboard XAML construction. The primary
/// Playnite page remains usable even when an experimental resource or host is unavailable.
/// </summary>
public sealed class UiFrameworkProbeLoader
{
    private readonly Action<Exception> logFailure;

    public UiFrameworkProbeLoader(Action<Exception> logFailure)
    {
        this.logFailure = logFailure ?? throw new ArgumentNullException(nameof(logFailure));
    }

    public bool TryCreate<T>(Func<T> create, out T? probe, out string failure)
        where T : class
    {
        if (create == null) throw new ArgumentNullException(nameof(create));

        try
        {
            probe = create();
            failure = string.Empty;
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
                Trace.TraceError("GameSaveCenter WPF-UI probe loader logging failed: {0}", loggingException);
            }

            probe = null;
            failure = "无法加载界面探针：" + exception.Message + "。维护中心仍可继续使用；可在修复环境后再次点击加载。";
            return false;
        }
    }
}
