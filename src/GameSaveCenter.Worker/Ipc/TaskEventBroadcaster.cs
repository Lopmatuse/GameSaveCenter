using System.Collections.Concurrent;
using System.Threading.Channels;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Worker.Ipc;

/// <summary>
/// In-memory fan-out for best-effort task notifications.
///
/// The task database and the request/response change feed remain the durable source
/// of truth. A slow or disconnected UI must never block a backup, restore, or media
/// operation, so each subscriber has a bounded drop-oldest queue.
/// </summary>
public sealed class TaskEventBroadcaster
{
    private const int PerSubscriberCapacity = 128;
    private readonly ConcurrentDictionary<Guid, Channel<TaskChangeEventDto>> subscribers = new();

    public TaskEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<TaskChangeEventDto>(new BoundedChannelOptions(PerSubscriberCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        if (!subscribers.TryAdd(id, channel)) throw new InvalidOperationException("Could not register task event subscriber.");
        return new TaskEventSubscription(id, channel.Reader, Unsubscribe);
    }

    public void Publish(TaskChangeEventDto change)
    {
        foreach (var subscriber in subscribers.Values)
        {
            subscriber.Writer.TryWrite(Clone(change));
        }
    }

    private void Unsubscribe(Guid id)
    {
        if (subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    private static TaskChangeEventDto Clone(TaskChangeEventDto change) => new()
    {
        Sequence = change.Sequence,
        Task = new TaskStatusDto
        {
            TaskId = change.Task.TaskId,
            TaskType = change.Task.TaskType,
            GameId = change.Task.GameId,
            GameName = change.Task.GameName,
            State = change.Task.State,
            ProgressPercent = change.Task.ProgressPercent,
            Message = change.Task.Message,
            CreatedUtc = change.Task.CreatedUtc,
            StartedUtc = change.Task.StartedUtc,
            FinishedUtc = change.Task.FinishedUtc,
            ErrorCode = change.Task.ErrorCode,
            ErrorMessage = change.Task.ErrorMessage
        }
    };
}

/// <summary>Owns one transient task-event subscription.</summary>
public sealed class TaskEventSubscription : IDisposable
{
    private readonly Guid id;
    private readonly Action<Guid> unsubscribe;
    private int disposed;

    internal TaskEventSubscription(Guid id, ChannelReader<TaskChangeEventDto> reader, Action<Guid> unsubscribe)
    {
        this.id = id;
        Reader = reader;
        this.unsubscribe = unsubscribe;
    }

    public ChannelReader<TaskChangeEventDto> Reader { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) unsubscribe(id);
    }
}
