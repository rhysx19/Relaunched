using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public class AppScanner : IAppScanner
    {
        private const int SLR_NO_UI = 0x0001;
        private const int SLGP_UNCPRIORITY = 0x0002;

        private readonly string? _simulatedPath;

        public AppScanner(string? simulatedPath = null)
        {
            _simulatedPath = simulatedPath;
        }

        public Task<List<AppItem>> ScanApplicationsAsync()
        {
            // The scan performs blocking disk I/O and COM interop, so it must not
            // run on the caller's (UI) thread. Shell COM objects are apartment
            // threaded, so on Windows the work runs on a dedicated STA thread.
            var tcs = new TaskCompletionSource<List<AppItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    tcs.SetResult(ScanApplications());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.IsBackground = true;
            if (OperatingSystem.IsWindows())
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
            thread.Start();
            return tcs.Task;
        }

        private List<AppItem> ScanApplications()
        {
            var allApps = new List<AppItem>();
            bool useSimulated = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !string.IsNullOrEmpty(_simulatedPath);

            if (useSimulated)
            {
                if (!string.IsNullOrEmpty(_simulatedPath) && Directory.Exists(_simulatedPath))
                {
                    try
                    {
                        var files = Directory.GetFiles(_simulatedPath, "*.lnk", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(file);
                            allApps.Add(new AppItem
                            {
                                Id = name.ToLowerInvariant().Replace(" ", "_"),
                                Name = name,
                                TargetPath = file,
                                IconPath = file + ".png",
                                IsFolder = false
                            });
                        }
                    }
                    catch
                    {
                        // Ignore directory scan errors
                    }
                }
            }
            else
            {
                // Windows behavior
                var paths = new List<string>();
                try
                {
                    var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    if (!string.IsNullOrEmpty(userStartMenu) && Directory.Exists(userStartMenu))
                    {
                        paths.Add(userStartMenu);
                    }
                }
                catch { }

                try
                {
                    var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                    if (!string.IsNullOrEmpty(commonStartMenu) && Directory.Exists(commonStartMenu))
                    {
                        paths.Add(commonStartMenu);
                    }
                }
                catch { }

                var iconCacheDir = GetIconCacheDir();
                foreach (var path in paths)
                {
                    try
                    {
                        var files = Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(file);
                            var id = name.ToLowerInvariant().Replace(" ", "_");
                            var (targetPath, iconFile, iconIndex) = ResolveLnk(file);

                            // Advertised/MSI shortcuts can report an empty target;
                            // launching the .lnk itself via the shell still works.
                            if (string.IsNullOrWhiteSpace(targetPath))
                            {
                                targetPath = file;
                            }

                            allApps.Add(new AppItem
                            {
                                Id = id,
                                Name = name,
                                TargetPath = targetPath,
                                IconPath = ExtractIconToCache(iconFile, iconIndex, targetPath, iconCacheDir, id),
                                IsFolder = false
                            });
                        }
                    }
                    catch
                    {
                        // Ignore directory scan errors
                    }
                }
            }

            // Post-processing: Filter, Deduplicate, Sort
            var filteredApps = allApps.Where(app => !IsFiltered(app.Name)).ToList();

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var finalList = new List<AppItem>();

            foreach (var app in filteredApps)
            {
                if (seenNames.Contains(app.Name) || seenTargetPaths.Contains(app.TargetPath))
                {
                    continue;
                }
                seenNames.Add(app.Name);
                seenTargetPaths.Add(app.TargetPath);
                finalList.Add(app);
            }

            var sortedList = finalList.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return sortedList;
        }

        private bool IsFiltered(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith(".")) return true;
            if (name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private (string TargetPath, string IconFile, int IconIndex) ResolveLnk(string lnkPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return (lnkPath, string.Empty, 0);
            }

            try
            {
                var link = (IShellLinkW)new ShellLink();
                var persistFile = (IPersistFile)link;
                persistFile.Load(lnkPath, 0); // STGM_READ is 0

                try
                {
                    // Resolve moved/renamed targets. SLR_NO_UI with a 100ms
                    // timeout in the high word keeps this silent and fast.
                    link.Resolve(IntPtr.Zero, SLR_NO_UI | (100 << 16));
                }
                catch
                {
                    // A dead link still carries its stored path data; fall through.
                }

                var target = new StringBuilder(1024);
                link.GetPath(target, target.Capacity, out _, SLGP_UNCPRIORITY);

                var iconPathSb = new StringBuilder(1024);
                link.GetIconLocation(iconPathSb, iconPathSb.Capacity, out int iconIndex);

                string targetStr = target.ToString();
                // Icon locations frequently use environment variables, e.g.
                // "%SystemRoot%\system32\shell32.dll".
                string iconStr = Environment.ExpandEnvironmentVariables(iconPathSb.ToString());

                if (string.IsNullOrWhiteSpace(iconStr))
                {
                    iconStr = targetStr;
                    iconIndex = 0;
                }

                return (targetStr, iconStr, iconIndex);
            }
            catch
            {
                return (lnkPath, string.Empty, 0);
            }
        }

        private static string GetIconCacheDir()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicLaunchpad", "IconCache");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // Extraction will simply fail and apps fall back to placeholders.
            }
            return dir;
        }

        /// <summary>
        /// Extracts the app icon to a cached PNG file and returns its path, so the
        /// UI can load it with a plain image decoder. Returns an empty string when
        /// no icon could be extracted (the UI renders a placeholder).
        /// </summary>
        private static string ExtractIconToCache(string iconFile, int iconIndex, string targetPath, string cacheDir, string appId)
        {
            if (!OperatingSystem.IsWindows())
            {
                return string.Empty;
            }

            try
            {
                return ExtractIconToCacheWindows(iconFile, iconIndex, targetPath, cacheDir, appId);
            }
            catch
            {
                return string.Empty;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string ExtractIconToCacheWindows(string iconFile, int iconIndex, string targetPath, string cacheDir, string appId)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                appId = appId.Replace(invalid, '_');
            }

            var pngPath = Path.Combine(cacheDir, appId + ".png");
            if (File.Exists(pngPath))
            {
                return pngPath;
            }

            System.Drawing.Icon? icon = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
                {
                    // Honors the icon index stored in the shortcut (a negative
                    // value is a resource id, matching GetIconLocation semantics).
                    icon = System.Drawing.Icon.ExtractIcon(iconFile, iconIndex, 256);
                }
            }
            catch
            {
                // Fall through to the associated-icon fallback below.
            }

            if (icon == null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
                    {
                        icon = System.Drawing.Icon.ExtractAssociatedIcon(targetPath);
                    }
                }
                catch
                {
                    // No icon available; the UI shows a placeholder.
                }
            }

            if (icon == null)
            {
                return string.Empty;
            }

            using (icon)
            using (var bitmap = icon.ToBitmap())
            {
                bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            return pngPath;
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
    }
}
