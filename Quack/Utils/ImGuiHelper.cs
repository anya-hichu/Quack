using ImGuiNET;

namespace Quack.Utils;

public class ImGuiHelper
{
    public static unsafe ImGuiListClipperPtr NewListClipper()
    {
        return new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
    }
}
