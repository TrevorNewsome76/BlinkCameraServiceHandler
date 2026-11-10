namespace BlinkCameraCall.IO;

public class ProgressSpinner(IConsoleIO io)
{
    private static readonly char[] Frames = ['|', '/', '-', '\\'];
    private readonly object _lock = new();
    private int _frameIndex;
    private bool _active;

    public void Start(string message)
    {
        lock (_lock)
        {
            io.Write(message + Environment.NewLine);
            _active = true;
            DrawFrame();
        }
    }

    public void Tick()
    {
        lock (_lock)
        {
            if (!_active)
                return;

            EraseFrame();
            _frameIndex = (_frameIndex + 1) % Frames.Length;
            DrawFrame();
        }
    }

    public void WriteLine(string text)
    {
        lock (_lock)
        {
            if (_active)
                EraseFrame();

            io.Write(text + Environment.NewLine);

            if (_active)
                DrawFrame();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_active)
                EraseFrame();

            _active = false;
        }
    }

    private void DrawFrame() => io.Write(Frames[_frameIndex].ToString());

    private void EraseFrame()
    {
        io.CursorLeft = Math.Max(0, io.CursorLeft - 1);
        io.Write(" ");
        io.CursorLeft = Math.Max(0, io.CursorLeft - 1);
    }
}
