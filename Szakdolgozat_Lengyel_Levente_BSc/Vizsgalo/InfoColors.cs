namespace Vizsgalo;

public static class InfoColors
{
    public const ConsoleColor RuntimeInfo = ConsoleColor.Blue;
    public const ConsoleColor ResponseResultText = ConsoleColor.DarkCyan;
    public const ConsoleColor ResponseCategory = ConsoleColor.DarkRed;
    public const ConsoleColor UserInputHeader = ConsoleColor.DarkYellow;
    public const ConsoleColor SummaryText = ConsoleColor.Yellow;
    public const ConsoleColor ScanningStartText = ConsoleColor.Cyan;
    public const ConsoleColor StatusError = ConsoleColor.Red;
    public const ConsoleColor StatusSuccess = ConsoleColor.DarkGreen;
    public const ConsoleColor Operations1 = ConsoleColor.Green;
    public const ConsoleColor Operations2 = ConsoleColor.Yellow;
    public const ConsoleColor Operations3 = ConsoleColor.Red;
    
    public static void WriteToConsole(ConsoleColor color, string data)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(data);
        Console.ResetColor();
    }
}