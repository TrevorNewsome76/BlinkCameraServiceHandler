namespace BlinkCameraCall.IO;

public interface IConsoleIO
{
    ConsoleKeyInfo ReadKey();
    bool KeyAvailable { get; }
    void Write(string text);
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
    int WindowWidth { get; }
}
