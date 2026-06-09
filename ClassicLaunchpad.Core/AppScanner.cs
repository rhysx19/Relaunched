using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public class AppScanner : IAppScanner
    {
        private readonly string? _simulatedPath;

        public AppScanner(string? simulatedPath = null)
        {
            _simulatedPath = simulatedPath;
        }

        public Task<List<AppItem>> ScanApplicationsAsync()
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

                foreach (var path in paths)
                {
                    try
                    {
                        var files = Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(file);
                            var (targetPath, iconPath) = ResolveLnk(file);
                            allApps.Add(new AppItem
                            {
                                Id = name.ToLowerInvariant().Replace(" ", "_"),
                                Name = name,
                                TargetPath = targetPath,
                                IconPath = iconPath,
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
            return Task.FromResult(sortedList);
        }

        private bool IsFiltered(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith(".")) return true;
            if (name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private (string TargetPath, string IconPath) ResolveLnk(string lnkPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return (lnkPath, lnkPath + ".png");
            }

            try
            {
                var link = (IShellLinkW)new ShellLink();
                var persistFile = (IPersistFile)link;
                persistFile.Load(lnkPath, 0); // STGM_READ is 0

                var target = new StringBuilder(260);
                var findData = new WIN32_FIND_DATAW();
                link.GetPath(target, target.Capacity, out findData, 2); // SLGP_UNCPRIORITY is 2

                var iconPathSb = new StringBuilder(260);
                link.GetIconLocation(iconPathSb, iconPathSb.Capacity, out int iconIndex);

                string targetStr = target.ToString();
                string iconStr = iconPathSb.ToString();

                if (string.IsNullOrWhiteSpace(iconStr))
                {
                    iconStr = targetStr;
                }

                return (targetStr, iconStr);
            }
            catch
            {
                return (lnkPath, lnkPath + ".png");
            }
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
