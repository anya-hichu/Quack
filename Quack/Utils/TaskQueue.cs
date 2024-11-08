using System;
using System.Threading.Tasks;

namespace Quack.Utils;

public class TaskQueue
{
    private Task Tail { get; set; } = Task.CompletedTask;

    public void Enqueue(Action action)
    {
        Tail = Tail.ContinueWith((_) => action());
    }
}
