using Quack.Utils;
using System;

namespace Quack.UI.Tabs;

public abstract class ModelTab(Debouncers debouncers)
{
    protected static string BLANK_NAME = "(Blank)";
    protected Debouncers Debouncers { get; init; } = debouncers;
    protected void Debounce(string key, Action action)
    {
        Debouncers.Invoke(key, action, TimeSpan.FromSeconds(1));
    }
}
