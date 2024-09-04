using System;
using System.Text.RegularExpressions;

namespace Quack.Utils;

public partial class PMFormatter : IFormatProvider, ICustomFormatter
{
    public object GetFormat(Type? type)
    {
        if (typeof(ICustomFormatter) == type)
            return this;
        else
            return null!;
    }

    [GeneratedRegexAttribute(@"^/")]
    private static partial Regex LeadingSlashGeneratedRegex();

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if(format == "P" && arg != null)
        {
            return LeadingSlashGeneratedRegex()
                .Replace($"{arg}", string.Empty)
                .Replace("<", "[")
                .Replace(">", "]");
        } 
        else if(format != null) 
        {
            return format.ToString(formatProvider);
        } 
        else if (arg != null)
        {
            return $"{arg}";
        } else
        {
            return string.Empty;
        }
    }
}
