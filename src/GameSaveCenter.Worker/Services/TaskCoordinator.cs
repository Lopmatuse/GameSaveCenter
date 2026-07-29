using System.Collections.Concurrent;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Serializes destructive work per game while allowing unrelated games to progress.</summary>
public sealed class TaskCoordinator
{
    private readonly SqliteStateStore _store;
    private readonly ILogger<TaskCoordinator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gameLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<TaskChangeEventDto> _changes = new();
    private readonly object _changeSignalGate = new();
    private TaskCompletionSource<bool> _changeSignal = NewChangeSignal();
    private long _changeSequence;
    private const int ChangeRetention = 500;

    public TaskCoordinator(SqliteStateStore store, ILogger<TaskCoordinator> logger)
    { _store=store; _logger=logger; }

    public async Task<TaskStatusDto> RunAsync(
        string taskType,
        string gameId,
        string gameName,
        Func<TaskProgress, CancellationToken, Task> operation,
        CancellationToken outerToken)
    {
        var task = new TaskStatusDto
        {
            TaskId=Guid.NewGuid().ToString("N"), TaskType=taskType, GameId=gameId, GameName=gameName,
            State=TaskState.Queued, ProgressPercent=0, Message="等待执行", CreatedUtc=DateTime.UtcNow
        };
        await PersistAndPublishAsync(task, outerToken).ConfigureAwait(false);
        var gate=_gameLocks.GetOrAdd(string.IsNullOrWhiteSpace(gameId)?"__global__":gameId,_=>new SemaphoreSlim(1,1));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        _taskTokens[task.TaskId]=linked;
        var gateEntered=false;
        try
        {
            await gate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateEntered=true;
            task.State=TaskState.Running;task.StartedUtc=DateTime.UtcNow;task.Message="正在执行";
            await PersistAndPublishAsync(task,linked.Token).ConfigureAwait(false);
            var progress=new TaskProgress(async (percent,message)=>
            {
                task.ProgressPercent=Math.Clamp(percent,0,100);task.Message=message;
                await PersistAndPublishAsync(task,CancellationToken.None).ConfigureAwait(false);
            });
            await operation(progress,linked.Token).ConfigureAwait(false);
            task.State=TaskState.Succeeded;
            task.ProgressPercent=100;
            if(string.IsNullOrWhiteSpace(task.Message) || string.Equals(task.Message,"正在执行",StringComparison.Ordinal)) task.Message="已完成";
            task.FinishedUtc=DateTime.UtcNow;
        }
        catch(OperationCanceledException)
        {
            task.State=TaskState.Cancelled;task.Message="已取消";task.FinishedUtc=DateTime.UtcNow;
        }
        catch(WorkerOperationException ex)
        {
            _logger.LogError(ex,"Task {TaskType} failed for {Game}: {Code}",taskType,gameName,ex.Code);
            task.State=TaskState.Failed;
            task.ErrorCode=ex.Code;
            task.ErrorMessage=string.IsNullOrWhiteSpace(ex.DiagnosticDetail)?ex.Message:$"{ex.Message} | {ex.DiagnosticDetail}";
            task.Message="执行失败";
            task.FinishedUtc=DateTime.UtcNow;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex,"Task {TaskType} failed for {Game}",taskType,gameName);
            task.State=TaskState.Failed;task.ErrorCode=ex.GetType().Name;task.ErrorMessage=ex.Message;task.Message="执行失败";task.FinishedUtc=DateTime.UtcNow;
        }
        finally
        {
            await PersistAndPublishAsync(task,CancellationToken.None).ConfigureAwait(false);
            _taskTokens.TryRemove(task.TaskId,out _);
            if(gateEntered)gate.Release();
        }
        return task;
    }

    public bool Cancel(string taskId)
    {
        if(!_taskTokens.TryGetValue(taskId,out var token)) return false;
        try{token.Cancel();return true;}
        catch(ObjectDisposedException){return false;}
    }

    public TaskChangeFeedDto GetChanges(long afterSequence,int limit)
    {
        // Progress callbacks can reach this queue concurrently. Normalize the small bounded
        // retention window so a scheduler interleave cannot make the first enqueued item look
        // newer than the actual oldest sequence.
        var snapshot=_changes.ToArray().OrderBy(x=>x.Sequence).ToArray();
        var oldest=snapshot.Length==0?Interlocked.Read(ref _changeSequence):snapshot[0].Sequence;
        var latest=Interlocked.Read(ref _changeSequence);
        var resetRequired=afterSequence>latest || (snapshot.Length>0 && afterSequence<oldest-1);
        var changes=resetRequired
            ? snapshot.Take(Math.Clamp(limit,1,500))
            : snapshot.Where(x=>x.Sequence>afterSequence).Take(Math.Clamp(limit,1,500));
        return new TaskChangeFeedDto{LatestSequence=latest,ResetRequired=resetRequired,Changes=changes.ToList()};
    }

    public async Task<TaskChangeFeedDto> WaitForChangesAsync(long afterSequence,int limit,int waitSeconds,CancellationToken token)
    {
        var current=GetChanges(afterSequence,limit);
        if(current.ResetRequired||current.Changes.Count>0||waitSeconds<=0)return current;

        Task signalTask;
        lock(_changeSignalGate) signalTask=_changeSignal.Task;

        // Close the small race between the first snapshot and subscribing to the signal.
        current=GetChanges(afterSequence,limit);
        if(current.ResetRequired||current.Changes.Count>0)return current;

        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(waitSeconds,1,25)));
        try{await signalTask.WaitAsync(timeout.Token).ConfigureAwait(false);}
        catch(OperationCanceledException) when(!token.IsCancellationRequested){/* Normal long-poll timeout. */}
        return GetChanges(afterSequence,limit);
    }

    private async Task PersistAndPublishAsync(TaskStatusDto task,CancellationToken token)
    {
        await _store.AddOrUpdateTaskAsync(task,token).ConfigureAwait(false);
        var sequence=Interlocked.Increment(ref _changeSequence);
        _changes.Enqueue(new TaskChangeEventDto{Sequence=sequence,Task=Clone(task)});
        while(_changes.Count>ChangeRetention && _changes.TryDequeue(out _)) { }
        TaskCompletionSource<bool> signal;
        lock(_changeSignalGate)
        {
            signal=_changeSignal;
            _changeSignal=NewChangeSignal();
        }
        signal.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> NewChangeSignal()
        =>new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskStatusDto Clone(TaskStatusDto task)=>new()
    {
        TaskId=task.TaskId,TaskType=task.TaskType,GameId=task.GameId,GameName=task.GameName,State=task.State,
        ProgressPercent=task.ProgressPercent,Message=task.Message,CreatedUtc=task.CreatedUtc,StartedUtc=task.StartedUtc,
        FinishedUtc=task.FinishedUtc,ErrorCode=task.ErrorCode,ErrorMessage=task.ErrorMessage
    };
}

/// <summary>Task progress sink safe for background callers.</summary>
public sealed class TaskProgress
{
    private readonly Func<int,string,Task> _report;
    public TaskProgress(Func<int,string,Task> report)=>_report=report;
    public Task ReportAsync(int percent,string message)=>_report(percent,message);
}
