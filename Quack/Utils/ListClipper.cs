using ImGuiNET;

namespace Quack.Utils;

public static class ListClipper
{
    public static unsafe ImGuiListClipperPtr Build()
    {
        return new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
    }
}
