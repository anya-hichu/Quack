using SQLite;

namespace Quack.Macros;

public class MacroRecord
{
    [Column("name")]
    public string? Name { get; set; }

    [Column("path")]
    public string? Path { get; set; }

    [Column("command")]
    public string? Command { get; set; }

    [Column("tags")]
    public string? Tags { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Column("loop")]
    public string? Loop { get; set; }
}
