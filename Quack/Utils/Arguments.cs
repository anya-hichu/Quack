// Source: https://jake.ginnivan.net/c-sharp-argument-parser/

using System;
using System.Text;

namespace Quack.Utils;

/// <summary>
/// Arguments class
/// </summary>
public class Arguments
{
    private static readonly char CUSTOM_SEPARATOR = '\n';

    /// <summary>
    /// Splits the command line. When main(string[] args) is used escaped quotes (ie a path "c:\folder\")
    /// Will consume all the following command line arguments as the one argument. 
    /// This function ignores escaped quotes making handling paths much easier.
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    /// <returns></returns>
    public static string[] SplitCommandLine(string commandLine)
    {
        var translatedArguments = new StringBuilder(commandLine);
        var escaped = false;
        for (var i = 0; i < translatedArguments.Length; i++)
        {
            if (translatedArguments[i] == '"')
            {
                escaped = !escaped;
            }
            if (translatedArguments[i] == ' ' && !escaped)
            {
                translatedArguments[i] = CUSTOM_SEPARATOR;
            }
        }

        var toReturn = translatedArguments.ToString().Split([CUSTOM_SEPARATOR], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < toReturn.Length; i++)
        {
            toReturn[i] = RemoveMatchingQuotes(toReturn[i]);
        }
        return toReturn;
    }

    public static string RemoveMatchingQuotes(string stringToTrim)
    {
        var firstQuoteIndex = stringToTrim.IndexOf('"');
        var lastQuoteIndex = stringToTrim.LastIndexOf('"');
        while (firstQuoteIndex != lastQuoteIndex)
        {
            stringToTrim = stringToTrim.Remove(firstQuoteIndex, 1);
            stringToTrim = stringToTrim.Remove(lastQuoteIndex - 1, 1);
            firstQuoteIndex = stringToTrim.IndexOf('"');
            lastQuoteIndex = stringToTrim.LastIndexOf('"');
        }

        return stringToTrim;
    }
}
