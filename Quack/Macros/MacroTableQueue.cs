using Quack.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quack.Macros;

public class MacroTableQueue(MacroTable macroTable, ActionQueue actionQueue)
{
    private MacroTable MacroTable { get; init; } = macroTable;
    private ActionQueue ActionQueue { get; init; } = actionQueue;

    public Task Insert(Macro macro)
    {
        var clone = Clone(macro);
        return ActionQueue.Enqueue(() => MacroTable.Insert(clone));
    }

    public Task Insert(IEnumerable<Macro> macros)
    {
        var clones = Clone(macros);
        return ActionQueue.Enqueue(() => MacroTable.Insert(clones));
    }

    public Task Update(string column, Macro macro)
    {
        var clone = Clone(macro);
        return ActionQueue.Enqueue(() => MacroTable.Update(column, clone));
    }

    public Task Update(string column, Macro macro, string path)
    {
        var clone = Clone(macro);
        return ActionQueue.Enqueue(() => MacroTable.Update(column, clone, path));
    }

    public Task Delete(Macro macro)
    {
        var clone = Clone(macro);
        return ActionQueue.Enqueue(() => MacroTable.Delete(clone));
    }

    public Task Delete(IEnumerable<Macro> macros)
    {
        var clones = Clone(macros);
        return ActionQueue.Enqueue(() => MacroTable.Delete(clones));
    }

    public Task DeleteAll()
    {
        return ActionQueue.Enqueue(() => MacroTable.DeleteAll());
    }

    private static Macro Clone(Macro macro)
    {
        return macro.Clone();
    }

    private static List<Macro> Clone(IEnumerable<Macro> macros)
    {
        return [.. macros.Select(Clone)];
    }
}
