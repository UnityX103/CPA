using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Best-effort cleanup when the Unity Editor is quitting.
    /// - Intentionally does NOT stop transports or local HTTP MCP server processes.
    ///   This allows externally started server processes to survive Unity shutdown.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpEditorShutdownCleanup
    {
        static McpEditorShutdownCleanup()
        {
            // Guard against duplicate subscriptions across domain reloads.
            try { EditorApplication.quitting -= OnEditorQuitting; } catch { }
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            // Keep server process alive across Unity shutdown by design.
        }
    }
}
