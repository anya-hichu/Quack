using System;
using System.Text.RegularExpressions;

namespace Quack.Utils;

public partial class PMFormatter : IFormatProvider, ICustomFormatter
{
    [GeneratedRegexAttribute(@"^/")]
    private static partial Regex LeadingSlashGeneratedRegex();

    public object GetFormat(Type? type)
    {
        return typeof(ICustomFormatter) == type ? this : null!;
    }

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if (format == "P" && arg != null)
        {
            // Puppet master doesn't provide a way to escape brackets, it will break
            return LeadingSlashGeneratedRegex()
                .Replace($"{arg}", string.Empty)
                .Replace("<", "[")
                .Replace(">", "]");
        } 
        else if (format != null) 
        {
            return format.ToString(formatProvider);
        } 
        else
        {
            return $"{arg}";
        }
    }
}
