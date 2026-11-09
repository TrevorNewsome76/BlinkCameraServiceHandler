using System.Text;

namespace BlinkCameraCall.IO;

/// <summary>
/// A line editor that reads a line of input one key at a time, supporting
/// cursor movement (Left/Right/Home/End/Delete) and recall of previously
/// entered lines (Up/Down), while keeping the cursor from ever moving back
/// over the prompt itself.
/// </summary>
public class ShellLineEditor(IConsoleIO io)
{
    private readonly List<string> _history = [];
    private readonly object _lock = new();
    private int? _historyIndex;

    private bool _active;
    private string _prompt = string.Empty;
    private StringBuilder _buffer = new();
    private int _cursorIndex;
    private int _promptStartTop;
    private int _promptEndLeft;
    private int _promptEndTop;

    public string ReadLine(string prompt)
    {
        _promptStartTop = io.CursorTop;
        _prompt = prompt;
        io.Write(prompt);
        _promptEndLeft = io.CursorLeft;
        _promptEndTop = io.CursorTop;

        _buffer = new StringBuilder();
        _cursorIndex = 0;
        _historyIndex = null;
        _active = true;

        lock (_lock)
        {
            // Establish the baseline cursor position explicitly rather than relying
            // on Write's side effect — every subsequent move is computed from here.
            MoveCursorTo(_cursorIndex);
        }

        while (true)
        {
            var key = io.ReadKey();

            lock (_lock)
            {
                if (!char.IsControl(key.KeyChar))
                {
                    _buffer.Insert(_cursorIndex, key.KeyChar);
                    _cursorIndex++;
                    RedrawTailFrom(_cursorIndex - 1);
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.Backspace:
                        if (_cursorIndex == 0)
                        {
                            break;
                        }

                        _buffer.Remove(_cursorIndex - 1, 1);
                        _cursorIndex--;
                        RedrawTailFrom(_cursorIndex);
                        break;

                    case ConsoleKey.Delete:
                        if (_cursorIndex == _buffer.Length)
                        {
                            break;
                        }

                        _buffer.Remove(_cursorIndex, 1);
                        RedrawTailFrom(_cursorIndex);
                        break;

                    case ConsoleKey.LeftArrow:
                        _cursorIndex = Math.Max(0, _cursorIndex - 1);
                        MoveCursorTo(_cursorIndex);
                        break;

                    case ConsoleKey.RightArrow:
                        _cursorIndex = Math.Min(_buffer.Length, _cursorIndex + 1);
                        MoveCursorTo(_cursorIndex);
                        break;

                    case ConsoleKey.Home:
                        _cursorIndex = 0;
                        MoveCursorTo(_cursorIndex);
                        break;

                    case ConsoleKey.End:
                        _cursorIndex = _buffer.Length;
                        MoveCursorTo(_cursorIndex);
                        break;

                    case ConsoleKey.UpArrow:
                    {
                        if (_history.Count == 0)
                        {
                            break;
                        }

                        if (_historyIndex is null)
                        {
                            _historyIndex = _history.Count - 1;
                        }
                        else if (_historyIndex > 0)
                        {
                            _historyIndex--;
                        }
                        else
                        {
                            break;
                        }

                        var previousLength = _buffer.Length;
                        _buffer.Clear();
                        _buffer.Append(_history[_historyIndex.Value]);
                        _cursorIndex = _buffer.Length;
                        RedrawAllFromStart(previousLength);
                        break;
                    }

                    case ConsoleKey.DownArrow:
                    {
                        if (_historyIndex is null)
                        {
                            break;
                        }

                        var previousLength = _buffer.Length;

                        if (_historyIndex < _history.Count - 1)
                        {
                            _historyIndex++;
                            _buffer.Clear();
                            _buffer.Append(_history[_historyIndex.Value]);
                        }
                        else
                        {
                            _historyIndex = null;
                            _buffer.Clear();
                        }

                        _cursorIndex = _buffer.Length;
                        RedrawAllFromStart(previousLength);
                        break;
                    }

                    case ConsoleKey.Enter:
                        io.Write(Environment.NewLine);
                        var result = _buffer.ToString();
                        if (result.Length > 0 && (_history.Count == 0 || _history[^1] != result))
                        {
                            _history.Add(result);
                        }

                        _active = false;
                        return result;
                }
            }
        }
    }

    /// <summary>
    /// Prints a message from outside the normal key-reading loop (e.g. a background
    /// watcher's alert) without corrupting an in-progress line: erases the prompt and
    /// whatever has been typed so far, writes the message, then redraws both.
    /// Assumes the prompt always starts at column 0, which holds for every caller in
    /// this app (each ReadLine follows a newline) — alerts don't need to track an
    /// arbitrary start column to stay correct.
    /// </summary>
    public void NotifyExternal(string message)
    {
        lock (_lock)
        {
            if (!_active)
            {
                io.Write(message + Environment.NewLine);
                return;
            }

            var totalLength = _prompt.Length + _buffer.Length;
            var rows = Math.Max(1, (totalLength + io.WindowWidth - 1) / io.WindowWidth);

            io.CursorLeft = 0;
            io.CursorTop = _promptStartTop;
            io.Write(new string(' ', rows * io.WindowWidth));

            io.CursorLeft = 0;
            io.CursorTop = _promptStartTop;
            io.Write(message + Environment.NewLine);

            _promptStartTop = io.CursorTop;
            io.Write(_prompt);
            _promptEndLeft = io.CursorLeft;
            _promptEndTop = io.CursorTop;
            io.Write(_buffer.ToString());

            MoveCursorTo(_cursorIndex);
        }
    }

    private void MoveCursorTo(int i)
    {
        var col = _promptEndLeft + i;
        var row = _promptEndTop + col / io.WindowWidth;
        col %= io.WindowWidth;
        io.CursorLeft = col;
        io.CursorTop = row;
    }

    private void RedrawTailFrom(int i)
    {
        io.Write(_buffer.ToString(i, _buffer.Length - i) + " ");
        MoveCursorTo(_cursorIndex);
    }

    // History recall can swap in a buffer of an arbitrarily different length
    // than what's currently on screen (unlike the tail redraws above, where
    // every caller changes the length by exactly one), so the erase has to
    // cover however much longer the previously-displayed line was.
    private void RedrawAllFromStart(int previousLength)
    {
        MoveCursorTo(0);
        var content = _buffer.ToString();
        var eraseCount = Math.Max(previousLength - content.Length, 0) + 1;
        io.Write(content + new string(' ', eraseCount));
        MoveCursorTo(_cursorIndex);
    }
}
