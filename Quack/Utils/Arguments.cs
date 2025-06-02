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
        char? quoteContext = null;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];

            if (escape)
            {
                currentArg.Append(ch);
                escape = false;
            }
            else if (ch == '\\')
            {
                escape = true;
            }
            else if ((ch == '"' || ch == '\''))
            {
                if (quoteContext == null)
                {
                    // Entering quoted context
                    quoteContext = ch;
                }
                else if (quoteContext == ch)
                {
                    // Possible escaped quote
                    if (i + 1 < commandLine.Length && commandLine[i + 1] == ch)
                    {
                        // Double quote character – escape it
                        currentArg.Append(ch);
                        i++; // Skip the next char
                    }
                    else
                    {
                        // Closing quote
                        quoteContext = null;
                    }
                }
                else
                {
                    // Nested quote of a different type
                    currentArg.Append(ch);
                }
            }
            else if (char.IsWhiteSpace(ch) && quoteContext == null)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(ch);
            }
        }

        if (escape)
        {
            currentArg.Append('\\');
        }

        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }

        return [.. args];
    }
}
