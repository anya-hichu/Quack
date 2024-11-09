using System;
using System.Threading.Tasks;

namespace Quack.Utils;

public class ActionQueue
{
    private Task Tail { get; set; } = Task.CompletedTask;
    public Task Enqueue(Action action)
    {
        return Tail = Tail.ContinueWith(_ => action());
    }
}
