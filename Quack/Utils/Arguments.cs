// Source: https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp

using System.Collections.Generic;
using System.Text;

namespace Quack.Utils;

/// <summary>
/// Arguments class
/// </summary>
public class Arguments
{
    public static string[] SplitCommandLine(string commandLine)
    {
        var args = new List<string>();
        var currentArg = new StringBuilder();
        var escape = false;
        var inQuote = false;
        var hadQuote = false;
        var prevCh = '\0';

        // Iterate all characters from the input string
        for (var i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];
            if (ch == '\\' && !escape)
            {
                // Beginning of a backslash-escape sequence
                escape = true;
            }
            else if (ch == '\\' && escape)
            {
                // Double backslash, keep one
                currentArg.Append(ch);
                escape = false;
            }
            else if (ch == '"' && !escape)
            {
                // Toggle quoted range
                inQuote = !inQuote;
                hadQuote = true;
                if (inQuote && prevCh == '"')
                {
                    // Doubled quote within a quoted range is like escaping
                    currentArg.Append(ch);
                }
            }
            else if (ch == '"' && escape)
            {
                // Backslash-escaped quote, keep it
                currentArg.Append(ch);
                escape = false;
            }
            else if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (escape)
                {
                    // Add pending escape char
                    currentArg.Append('\\');
                    escape = false;
                }
                // Accept empty arguments only if they are quoted
                if (currentArg.Length > 0 || hadQuote)
                {
                    args.Add(currentArg.ToString());
                }
                // Reset for next argument
                currentArg.Clear();
                hadQuote = false;
            }
            else
            {
                if (escape)
                {
                    // Add pending escape char
                    currentArg.Append('\\');
                    escape = false;
                }
                // Copy character from input, no special meaning
                currentArg.Append(ch);
            }
            prevCh = ch;
        }
        // Save last argument
        if (currentArg.Length > 0 || hadQuote)
        {
            args.Add(currentArg.ToString());
        }
        return [.. args];
    }
}
