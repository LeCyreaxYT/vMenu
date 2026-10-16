namespace vMenu.Enhanced.Data.Ticks;

public enum TickLog
{
    Debug,
    Info,
    Warn,
    Error,
}

// Every named loop on one side, so each one can be named, gated and stopped. Neither runtime has a
// tick registration that awaits its handler: ScheduleRepeated takes an Action, so an async Task
// handler re-arms the timer at its first await and the next invocation starts while the previous is
// still suspended. That leaves driving the loop by hand, which TickHandle does. Both sides own an
// instance and supply their own waiting, logging and profiler scopes.
public sealed class TickEngine(
    Func<long, Task> delay,
    Func<Task> yield,
    Action<TickLog, string> write,
    Func<bool> isProfilerRecording,
    Action<string> enterScope,
    Action exitScope,
    Func<bool> isMainThread,
    Action<Action> scheduleOnMainThread)
{
    private readonly List<TickHandle> _registered = [];

    public IReadOnlyList<TickHandle> Handles => _registered;

    // A single Reevaluate pass raises this once per tick it flips, so a subscriber doing real work
    // should coalesce.
    public event Action? Changed;

    // The condition is re-run by Reevaluate. When it is null the tick answers to TickHandle.Start and
    // TickHandle.Stop instead, and autoStart is ignored whenever a condition is set.
    public TickHandle Register(
        string name,
        Func<Task> handler,
        TickRate rate = default,
        Func<bool>? condition = null,
        Action? onStarted = null,
        Action? onStopped = null,
        bool autoStart = true)
    {
        var handle = new TickHandle(this, name, handler, rate, condition, autoStart)
        {
            OnStarted = onStarted,
            OnStopped = onStopped,
        };

        _registered.Add(handle);

        handle.Apply();

        NotifyChanged();

        return handle;
    }

    // Wrapped once here rather than once per iteration.
    public TickHandle Register(
        string name,
        Action handler,
        TickRate rate = default,
        Func<bool>? condition = null,
        Action? onStarted = null,
        Action? onStopped = null,
        bool autoStart = true)
    {
        return Register(
            name,
            () =>
            {
                handler();

                return Task.CompletedTask;
            },
            rate,
            condition,
            onStarted,
            onStopped,
            autoStart);
    }

    public void Reevaluate()
    {
        // Indexed, because a condition is caller code and one that registers or disposes a tick would
        // invalidate the enumerator mid pass.
        for (var i = 0; i < _registered.Count; i++)
        {
            _registered[i].Apply();
        }
    }

    public IEnumerable<string> Describe()
    {
        foreach (var handle in _registered)
        {
            yield return $"{handle.Name} ({handle.Rate}): {(handle.IsRunning ? "running" : "stopped")}";
        }
    }

    internal Task DelayAsync(long milliseconds) => delay(milliseconds);

    internal Task YieldAsync() => yield();

    internal MainThreadHop MainThreadAsync() => new(isMainThread, scheduleOnMainThread);

    internal bool IsMainThread => isMainThread();

    internal void Log(TickLog level, string message) => write(level, message);

    // Scope only the initial synchronous execution. A Task may suspend or continue on another
    // thread; keeping the native scope open across that wait attributes unrelated work to this tick.
    internal Task InvokeHandler(string scope, Func<Task> handler)
    {
        if (!isProfilerRecording())
        {
            return handler();
        }

        enterScope(scope);

        try
        {
            return handler();
        }
        finally
        {
            exitScope();
        }
    }

    internal void Unregister(TickHandle handle)
    {
        if (_registered.Remove(handle))
        {
            NotifyChanged();
        }
    }

    // A throwing subscriber must not abort the state change that raised the event.
    internal void NotifyChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Log(TickLog.Error, $"a change subscriber threw: {exception}");
        }
    }
}
