

using Dalamud;
using Dalamud.Plugin.Ipc.Internal;
using Newtonsoft.Json;

namespace Quack.Generators;

public static class GeneratorIpcCaller
{
    // Use lowercase name for javascript syntax
    public static object call(string name, params object[] args)
    {
        if (!Service<CallGate>.Get().Gates.TryGetValue(name, out var channel))
        {
            throw new GeneratorException($"Could not find IPC channel: {name}");
        }
        var returnValue = channel.InvokeFunc<object?>(args);
        return returnValue != null && returnValue.GetType().IsGenericType ? returnValue : returnValue?.ToString()!;
    }
}
