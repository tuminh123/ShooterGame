using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShooterGame.Editor
{
    // Make repository-local tools visible to this Editor process, including when
    // Unity Hub did not inherit their paths. Nothing is installed into the OS PATH.
    internal static class McpLocalEnvironment
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Configure();
        }

        [MenuItem("Shooter Game/MCP/Refresh Local Tools")]
        public static void RefreshLocalTools()
        {
            if (Configure())
                Debug.Log("[MCP] Local Python and uv are available to Unity. Click Refresh in MCP Setup.");
            else
                Debug.LogWarning("[MCP] Run Tools/MCP/Setup.ps1 first. Local tool setup supports Windows x64.");
        }

        private static bool Configure()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return false;

            string root = Path.GetDirectoryName(Application.dataPath);
            string tools = Path.Combine(root, ".mcp-tools");
            string uvDirectory = Path.Combine(tools, "uv");
            string pythonDirectory = Path.Combine(root, "Tools", "MCP", ".venv", "Scripts");
            if (!File.Exists(Path.Combine(uvDirectory, "uv.exe")) ||
                !File.Exists(Path.Combine(pythonDirectory, "python.exe")))
                return false;

            PrependPath(uvDirectory);
            PrependPath(pythonDirectory);
            Environment.SetEnvironmentVariable("UV_CACHE_DIR", Path.Combine(tools, "cache"));
            // Nút Start Server trong cửa sổ MCP gọi `uvx`; mặc định uvx khóa thư mục tool toàn cục
            // %APPDATA%\uv\tools và lỗi "Access is denied" nếu file .lock ở đó bị chặn quyền.
            // Giữ thư mục tool trong .mcp-tools (đã gitignore) để không phụ thuộc trạng thái máy.
            Environment.SetEnvironmentVariable("UV_TOOL_DIR", Path.Combine(tools, "tools"));
            Environment.SetEnvironmentVariable("UV_TOOL_BIN_DIR", Path.Combine(tools, "tools", "bin"));
            Environment.SetEnvironmentVariable("UV_PYTHON_INSTALL_DIR", Path.Combine(tools, "python"));
            Environment.SetEnvironmentVariable("UNITY_MCP_DISABLE_TELEMETRY", "true");
            return true;
        }

        private static void PrependPath(string directory)
        {
            string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string entry in current.Split(Path.PathSeparator))
            {
                if (string.Equals(entry.Trim().Trim('"').TrimEnd('\\', '/'),
                    directory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    return;
            }

            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + current);
        }
    }
}
