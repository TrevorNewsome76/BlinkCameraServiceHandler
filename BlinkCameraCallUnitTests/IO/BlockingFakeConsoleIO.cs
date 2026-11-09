using System.Collections.Concurrent;
using BlinkCameraCall.IO;

namespace BlinkCameraCallUnitTests.IO;

/// <summary>
/// Test double for <see cref="IConsoleIO"/> whose <see cref="ReadKey"/> blocks on a
/// real queue instead of returning instantly, so a test can run <c>ReadLine</c> on a
/// background thread and deterministically pause it mid-line to exercise a concurrent
/// writer (e.g. <c>ShellLineEditor.NotifyExternal</c>) without sleep-based guessing.
/// </summary>
public class BlockingFakeConsoleIO(int windowWidth = 80) : IConsoleIO
{
    private readonly BlockingCollection<ConsoleKeyInfo> _keys = new();
    private readonly SemaphoreSlim _readyForNextKey = new(0);
    private int _cursorLeft;
    private int _cursorTop;

    public List<string> WrittenText { get; } = [];

    public int CursorLeft
    {
        get => _cursorLeft;
        set => _cursorLeft = value;
    }

    public int CursorTop
    {
        get => _cursorTop;
        set => _cursorTop = value;
    }

    public int WindowWidth { get; } = windowWidth;

    public void Enqueue(ConsoleKeyInfo key) => _keys.Add(key);

    public ConsoleKeyInfo ReadKey()
    {
        _readyForNextKey.Release();
        return _keys.Take();
    }

    public void Write(string text) => WrittenText.Add(text);

    /// <summary>
    /// Blocks until <see cref="ReadKey"/> has been entered <paramref name="count"/>
    /// times since the editor started reading the line — i.e. until every key
    /// enqueued so far has been fully processed and the editor is idle waiting for
    /// the next one.
    /// </summary>
    public void WaitForReadKeyCalls(int count, TimeSpan timeout)
    {
        for (var i = 0; i < count; i++)
            if (!_readyForNextKey.Wait(timeout))
                throw new TimeoutException("ShellLineEditor never became idle waiting for the next key.");
    }
}
