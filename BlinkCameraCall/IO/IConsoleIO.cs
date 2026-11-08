namespace BlinkCameraCall.IO;

public interface IConsoleIO
{
    ConsoleKeyInfo ReadKey();
    void Write(string text);
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
    int WindowWidth { get; }
}
