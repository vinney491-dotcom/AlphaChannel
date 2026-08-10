using AlphaChannel.Plugin.PluginHub;
using Dalamud.Plugin.Ipc;

namespace AlphaChannel.Plugin;

// Opens Linkpearl's Messages app via IPC when Chat is pressed. Falls back to OpenMainUi if the
// OpenApp provider isn't registered yet (older Linkpearl builds).
internal static class LinkpearlBridge
{
    private const string InternalName = "Linkpearl";
    private const string OpenAppGate = "Linkpearl.OpenApp";
    private const string MessagesAppId = "message";

    private static ICallGateSubscriber<string, bool>? openApp;

    internal static bool TryOpenMessages()
    {
        try
        {
            openApp ??= Plugin.PluginInterface.GetIpcSubscriber<string, bool>(OpenAppGate);
            if (openApp.InvokeFunc(MessagesAppId))
            {
                return true;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Linkpearl] OpenApp IPC failed: {exception.Message}");
        }

        if (InstalledPluginsReader.TryToggle(InternalName))
        {
            Plugin.ChatGui.Print(
                "[AlphaChannel] Opened Linkpearl — open Messages on the phone for chat.");
            return true;
        }

        Plugin.ChatGui.Print(
            "[AlphaChannel] Linkpearl isn't loaded. Enable it in /xlplugins to use Chat.");
        return false;
    }
}
