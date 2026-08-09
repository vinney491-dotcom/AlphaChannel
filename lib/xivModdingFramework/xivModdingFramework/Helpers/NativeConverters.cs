// Cross-platform helper binary resolution for TexTools converters.
// Copyright © TexTools / AlphaChannel — GPL-3.0

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace xivModdingFramework.Helpers
{
    /// <summary>
    /// Resolves Windows .exe converter tools, with hooks for native Linux replacements later.
    /// </summary>
    public static class NativeConverters
    {
        public static string ResolveExecutable(string baseDirectory, string windowsExeName, string linuxBinaryName = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = AppContext.BaseDirectory;
            }

            linuxBinaryName ??= Path.GetFileNameWithoutExtension(windowsExeName);

            if (PlatformPaths.IsLinux || PlatformPaths.IsMacOS)
            {
                // Prefer a native sibling binary if present (future: ship linux builds of texconv/etc).
                var native = Path.Combine(baseDirectory, linuxBinaryName);
                if (File.Exists(native)) return native;

                var nativePath = Which(linuxBinaryName);
                if (nativePath != null) return nativePath;

                // Fall back to the Windows exe (caller may still run it under Wine if configured).
            }

            var win = Path.Combine(baseDirectory, windowsExeName);
            if (File.Exists(win)) return win;

            // Also accept forward-slash style joins from older code.
            win = Path.Combine(baseDirectory.Replace('\\', Path.DirectorySeparatorChar), windowsExeName);
            return win;
        }

        public static ProcessStartInfo CreateStartInfo(string fileName, string arguments, string workingDirectory)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // If we're on Linux and the target is still a .exe, optionally wrap with wine.
            if ((PlatformPaths.IsLinux || PlatformPaths.IsMacOS) &&
                fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var wine = Environment.GetEnvironmentVariable("TEXTOOLS_WINE")
                           ?? Which("wine");
                if (wine != null)
                {
                    psi.Arguments = $"\"{fileName}\" {arguments}";
                    psi.FileName = wine;
                }
            }

            return psi;
        }

        private static string Which(string name)
        {
            try
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    var candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }
    }
}
