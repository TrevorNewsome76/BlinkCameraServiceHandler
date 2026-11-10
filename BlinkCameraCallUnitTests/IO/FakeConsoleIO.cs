using BlinkCameraCall.IO;

namespace BlinkCameraCallUnitTests.IO;

/// <summary>
/// Test double for <see cref="IConsoleIO"/>: a scripted queue of keys to read,
/// plus a record of everything written and every cursor position set.
/// </summary>
public class FakeConsoleIO : IConsoleIO
{
    private readonly Queue<ConsoleKeyInfo> _keys;
    private int _cursorLeft;
    private int _cursorTop;

    public FakeConsoleIO(IEnumerable<ConsoleKeyInfo> keys, int windowWidth = 80)
    {
        _keys = new Queue<ConsoleKeyInfo>(keys);
        WindowWidth = windowWidth;
    }

    public List<string> WrittenText { get; } = [];

    public List<(int Left, int Top)> CursorPositionsSet { get; } = [];

    public int CursorLeft
    {
        get => _cursorLeft;
        set
        {
            _cursorLeft = value;
            CursorPositionsSet.Add((_cursorLeft, _cursorTop));
        }
    }

    public int CursorTop
    {
        get => _cursorTop;
        set
        {
            _cursorTop = value;
            CursorPositionsSet.Add((_cursorLeft, _cursorTop));
        }
    }

    public int WindowWidth { get; }

    public bool KeyAvailable { get; set; }

    public ConsoleKeyInfo ReadKey()
    {
        if (_keys.Count == 0)
        {
            throw new InvalidOperationException("FakeConsoleIO key queue is empty — the test script ran out of keys.");
        }

        return _keys.Dequeue();
    }

    public void Write(string text) => WrittenText.Add(text);
}
