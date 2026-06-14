namespace UltraDecompiler.Disassembly.Common;

/// <summary>ANSI escape-коды для раскраски вывода дизассемблера в терминале.</summary>
public static class Ansi
{
    public const string Reset = "\u001b[0m";
    public const string Gray = "\u001b[90m";
    public const string Red = "\u001b[91m";
    public const string Green = "\u001b[92m";
    public const string Yellow = "\u001b[93m";
    public const string Blue = "\u001b[94m";
    public const string Pink = "\u001b[95m";
    public const string Cyan = "\u001b[96m";

    public static string Wrap(string color, string text) => color + text + Reset;
}
