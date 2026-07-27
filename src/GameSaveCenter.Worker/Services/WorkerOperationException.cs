namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Operational failure with a stable user-facing code. The coordinator persists
/// the code and message so the Playnite task page can show the real cause instead
/// of a generic exception type.
/// </summary>
public sealed class WorkerOperationException : Exception
{
    public WorkerOperationException(string code, string message, string? diagnosticDetail = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "WORKER_OPERATION_FAILED" : code;
        DiagnosticDetail = diagnosticDetail ?? string.Empty;
    }

    public string Code { get; }
    public string DiagnosticDetail { get; }
}
