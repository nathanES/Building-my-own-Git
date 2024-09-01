using System.Text.Json;

namespace codecrafters_git.Utils;

public class CustomWriter : ICustomWriter
{
    public void WriteLine(string content)
    {
        Console.WriteLine(content);
    }

    public void WriteLine(object content)
    {
        Console.WriteLine(JsonSerializer.Serialize(content, options: new JsonSerializerOptions(){ WriteIndented = true}));
    }
    public void Write(string content)
    {
        Console.Write(content);
    }

    public void Write(object content)
    {
        Console.Write(JsonSerializer.Serialize(content, options: new JsonSerializerOptions(){ WriteIndented = true}));
    }
}

public interface ICustomWriter
{
    void WriteLine(string content);
    void WriteLine(object content);
    void Write(string content);
    void Write(object content);
}