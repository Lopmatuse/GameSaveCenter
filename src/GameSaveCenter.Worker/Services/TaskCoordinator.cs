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
        await _store.AddOrUpdateTaskAsync(task, outerToken).ConfigureAwait(false);
        var gate=_gameLocks.GetOrAdd(string.IsNullOrWhiteSpace(gameId)?"__global__":gameId,_=>new SemaphoreSlim(1,1));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        _taskTokens[task.TaskId]=linked;
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            task.State=TaskState.Running;task.StartedUtc=DateTime.UtcNow;task.Message="正在执行";
            await _store.AddOrUpdateTaskAsync(task,linked.Token).ConfigureAwait(false);
            var progress=new TaskProgress(async (percent,message)=>
            {
                task.ProgressPercent=Math.Clamp(percent,0,100);task.Message=message;
                await _store.AddOrUpdateTaskAsync(task,CancellationToken.None).ConfigureAwait(false);
            });
            await operation(progress,linked.Token).ConfigureAwait(false);
            task.State=TaskState.Succeeded;task.ProgressPercent=100;task.Message="已完成";task.FinishedUtc=DateTime.UtcNow;
        }
        catch(OperationCanceledException)
        {
            task.State=TaskState.Cancelled;task.Message="已取消";task.FinishedUtc=DateTime.UtcNow;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex,"Task {TaskType} failed for {Game}",taskType,gameName);
            task.State=TaskState.Failed;task.ErrorCode=ex.GetType().Name;task.ErrorMessage=ex.Message;task.Message="执行失败";task.FinishedUtc=DateTime.UtcNow;
        }
        finally
        {
            await _store.AddOrUpdateTaskAsync(task,CancellationToken.None).ConfigureAwait(false);
            _taskTokens.TryRemove(task.TaskId,out _);gate.Release();
        }
        return task;
    }

    public bool Cancel(string taskId)
    {
        if(!_taskTokens.TryGetValue(taskId,out var token)) return false;
        token.Cancel();return true;
    }
}

/// <summary>Task progress sink safe for background callers.</summary>
public sealed class TaskProgress
{
    private readonly Func<int,string,Task> _report;
    public TaskProgress(Func<int,string,Task> report)=>_report=report;
    public Task ReportAsync(int percent,string message)=>_report(percent,message);
}
