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
    private int? _historyIndex;

    public string ReadLine(string prompt)
    {
        io.Write(prompt);
        var promptEndLeft = io.CursorLeft;
        var promptEndTop = io.CursorTop;

        var buffer = new StringBuilder();
        var cursorIndex = 0;
        _historyIndex = null;

        void MoveCursorTo(int i)
        {
            var col = promptEndLeft + i;
            var row = promptEndTop + col / io.WindowWidth;
            col %= io.WindowWidth;
            io.CursorLeft = col;
            io.CursorTop = row;
        }

        void RedrawTailFrom(int i)
        {
            io.Write(buffer.ToString(i, buffer.Length - i) + " ");
            MoveCursorTo(cursorIndex);
        }

        // History recall can swap in a buffer of an arbitrarily different length
        // than what's currently on screen (unlike the tail redraws above, where
        // every caller changes the length by exactly one), so the erase has to
        // cover however much longer the previously-displayed line was.
        void RedrawAllFromStart(int previousLength)
        {
            MoveCursorTo(0);
            var content = buffer.ToString();
            var eraseCount = Math.Max(previousLength - content.Length, 0) + 1;
            io.Write(content + new string(' ', eraseCount));
            MoveCursorTo(cursorIndex);
        }

        // Establish the baseline cursor position explicitly rather than relying
        // on Write's side effect — every subsequent move is computed from here.
        MoveCursorTo(cursorIndex);

        while (true)
        {
            var key = io.ReadKey();

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Insert(cursorIndex, key.KeyChar);
                cursorIndex++;
                RedrawTailFrom(cursorIndex - 1);
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Backspace:
                    if (cursorIndex == 0)
                    {
                        break;
                    }

                    buffer.Remove(cursorIndex - 1, 1);
                    cursorIndex--;
                    RedrawTailFrom(cursorIndex);
                    break;

                case ConsoleKey.Delete:
                    if (cursorIndex == buffer.Length)
                    {
                        break;
                    }

                    buffer.Remove(cursorIndex, 1);
                    RedrawTailFrom(cursorIndex);
                    break;

                case ConsoleKey.LeftArrow:
                    cursorIndex = Math.Max(0, cursorIndex - 1);
                    MoveCursorTo(cursorIndex);
                    break;

                case ConsoleKey.RightArrow:
                    cursorIndex = Math.Min(buffer.Length, cursorIndex + 1);
                    MoveCursorTo(cursorIndex);
                    break;

                case ConsoleKey.Home:
                    cursorIndex = 0;
                    MoveCursorTo(cursorIndex);
                    break;

                case ConsoleKey.End:
                    cursorIndex = buffer.Length;
                    MoveCursorTo(cursorIndex);
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

                    var previousLength = buffer.Length;
                    buffer.Clear();
                    buffer.Append(_history[_historyIndex.Value]);
                    cursorIndex = buffer.Length;
                    RedrawAllFromStart(previousLength);
                    break;
                }

                case ConsoleKey.DownArrow:
                {
                    if (_historyIndex is null)
                    {
                        break;
                    }

                    var previousLength = buffer.Length;

                    if (_historyIndex < _history.Count - 1)
                    {
                        _historyIndex++;
                        buffer.Clear();
                        buffer.Append(_history[_historyIndex.Value]);
                    }
                    else
                    {
                        _historyIndex = null;
                        buffer.Clear();
                    }

                    cursorIndex = buffer.Length;
                    RedrawAllFromStart(previousLength);
                    break;
                }

                case ConsoleKey.Enter:
                    io.Write(Environment.NewLine);
                    var result = buffer.ToString();
                    if (result.Length > 0 && (_history.Count == 0 || _history[^1] != result))
                    {
                        _history.Add(result);
                    }

                    return result;
            }
        }
    }
}
