using vMenu.Enhanced.Data.Ticks;

namespace vMenu.TickChecks;

internal static class Program
{
    private static async Task Main()
    {
        var idle = new Harness();
        var original = new TaskCompletionSource();
        var returned = idle.Engine.InvokeHandler("idle", () => original.Task);
        Check(ReferenceEquals(returned, original.Task), "Inactive profiling must preserve the handler Task.");
        Check(idle.RecordingChecks == 1 && idle.Entries == 0 && idle.Exits == 0,
            "Inactive profiling must skip both scope native calls.");
        original.SetResult();
        await returned;
        Console.WriteLine("PASS: inactive profiling skips scope natives and preserves Task identity");

        var active = new Harness { Recording = true };
        var synchronous = active.Engine.InvokeHandler("synchronous", () =>
        {
            Check(active.Depth == 1, "Synchronous handler work must be inside its scope.");
            return Task.CompletedTask;
        });
        await synchronous;
        Check(active.Depth == 0 && active.Entries == 1 && active.Exits == 1, "Synchronous scope is unbalanced.");
        Console.WriteLine("PASS: active profiling brackets synchronous handler work");

        try
        {
            _ = active.Engine.InvokeHandler("throwing", () => throw new InvalidOperationException("sync failure"));
            throw new Exception("Synchronous handler failure was swallowed.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "sync failure")
        {
            Check(active.Depth == 0 && active.Entries == 2 && active.Exits == 2,
                "A throwing handler must close its profiler scope.");
        }
        Console.WriteLine("PASS: synchronous failures propagate with balanced scopes");

        var firstRelease = new TaskCompletionSource();
        var secondRelease = new TaskCompletionSource();
        var first = active.Engine.InvokeHandler("first", async () =>
        {
            await firstRelease.Task;
            Check(active.Depth == 0, "A resumed handler must not inherit a suspended profiler scope.");
        });
        Check(active.Depth == 0 && !first.IsCompleted, "Profiler scope crossed the first handler's await.");
        var second = active.Engine.InvokeHandler("second", async () =>
        {
            await secondRelease.Task;
            Check(active.Depth == 0, "Independent handlers must not leave overlapping scopes.");
        });
        Check(active.Depth == 0 && !second.IsCompleted && active.Entries == active.Exits,
            "Suspended handlers corrupted the shared profiler stack.");
        secondRelease.SetResult();
        firstRelease.SetResult();
        await Task.WhenAll(first, second);
        Console.WriteLine("PASS: concurrent suspended handlers close scopes before either continuation");

        var failure = new TaskCompletionSource();
        var failing = active.Engine.InvokeHandler("async failure", () => failure.Task);
        Check(active.Depth == 0, "A pending faulted handler still holds its scope.");
        failure.SetException(new InvalidOperationException("async failure"));
        try
        {
            await failing;
            throw new Exception("Asynchronous handler failure was swallowed.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "async failure")
        {
            Check(active.Depth == 0 && active.Entries == active.Exits, "Asynchronous failure unbalanced scopes.");
        }
        Console.WriteLine("PASS: asynchronous failures retain their original exception");

        var lifecycle = new Harness { Recording = true };
        var handlerRelease = new TaskCompletionSource();
        var calls = 0;
        using var handle = lifecycle.Engine.Register("lifecycle", async () =>
        {
            calls++;
            await handlerRelease.Task;
            Check(lifecycle.Depth == 0, "TickHandle kept its native scope open across an await.");
        });
        Check(calls == 0 && lifecycle.Yields.Count == 1, "A registered handler must wait for its first tick.");
        lifecycle.Yields.Dequeue().SetResult();
        Check(calls == 1 && lifecycle.Depth == 0 && lifecycle.Delays.Count == 0,
            "TickHandle must await the original handler without rearming or holding its scope.");
        handlerRelease.SetResult();
        Check(lifecycle.Delays.Count == 1, "The next iteration must be scheduled after handler completion.");
        handle.Stop();
        lifecycle.Delays.Dequeue().SetResult();
        Check(calls == 1 && !handle.IsRunning && lifecycle.Errors.Count == 0,
            "Stopping the tick must preserve its existing lifecycle and prevent another invocation.");
        Console.WriteLine("PASS: real TickHandle preserves wait, no-overlap and stop behavior");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private sealed class Harness
    {
        internal bool Recording;
        internal int RecordingChecks;
        internal int Depth;
        internal int Entries;
        internal int Exits;
        internal Queue<TaskCompletionSource> Yields { get; } = new();
        internal Queue<TaskCompletionSource> Delays { get; } = new();
        internal List<string> Errors { get; } = new();
        internal TickEngine Engine { get; }

        internal Harness()
        {
            Engine = new TickEngine(
                _ =>
                {
                    var completion = new TaskCompletionSource();
                    Delays.Enqueue(completion);
                    return completion.Task;
                },
                () =>
                {
                    var completion = new TaskCompletionSource();
                    Yields.Enqueue(completion);
                    return completion.Task;
                },
                (_, message) => Errors.Add(message),
                () =>
                {
                    RecordingChecks++;
                    return Recording;
                },
                _ =>
                {
                    Check(Depth == 0, "A previous handler left a native profiler scope open.");
                    Entries++;
                    Depth++;
                },
                () =>
                {
                    Check(Depth == 1, "Unexpected profiler scope exit.");
                    Exits++;
                    Depth--;
                },
                () => true,
                action => action());
        }
    }
}
