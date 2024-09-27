using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IntelOrca.OpenLauncher.Core
{
    public class BuildAsset
    {
        public string Name { get; }
        public Uri Uri { get; }
        public string ContentType { get; }
        public int Size { get; }

        public BuildAsset(string name, Uri uri, string contentType, int size)
        {
            Name = name;
            Uri = uri;
            ContentType = contentType;
            Size = size;
        }

        public override string ToString() => Name;

        public bool IsPortable
        {
            get
            {
                if (Name.Contains("installer", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("symbols", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("winnt", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            }
        }

        public bool IsAppImage => Name.Contains("AppImage", StringComparison.OrdinalIgnoreCase);

        public OSPlatform? Platform
        {
            get
            {
                if (Name.Contains("windows", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("win", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return OSPlatform.Windows;
                }
                else if (Name.Contains("macos", StringComparison.OrdinalIgnoreCase))
                {
                    return OSPlatform.OSX;
                }
                else if (Name.Contains("linux", StringComparison.OrdinalIgnoreCase))
                {
                    return OSPlatform.Linux;
                }
                return null;
            }
        }

        public Architecture? Arch
        {
            get
            {
                if (Name.Contains("x64", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("x86_64", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.X64;
                }
                else if (
                    Name.Contains("x86", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("win32", StringComparison.OrdinalIgnoreCase) ||
                    Name.Contains("i686", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.X86;
                }
                else if (Name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.Arm64;
                }
                else if (Name.Contains("arm", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.Arm;
                }
                return null;
            }
        }

        public bool IsApplicableForCurrentPlatform()
        {
            if (Platform != null && Platform != BuildAssetComparer.CurrentPlatform)
                return false;
            if (Arch != null && Arch != BuildAssetComparer.CurrentArch)
            {
                if (BuildAssetComparer.CurrentArch == Architecture.X64 &&
                    Arch == Architecture.X86)
                {
                    return true;
                }
                if (BuildAssetComparer.CurrentArch == Architecture.Arm64 &&
                    Arch == Architecture.Arm)
                {
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    public class BuildAssetComparer : IComparer<BuildAsset>
    {
        internal static OSPlatform CurrentPlatform = GetCurrentOS();
        internal static Architecture CurrentArch = RuntimeInformation.OSArchitecture;

        public static BuildAssetComparer Default = new BuildAssetComparer();

        public int Compare(BuildAsset? x, BuildAsset? y)
        {
            if (x == null || y == null)
            {
                throw new ArgumentNullException("BuildAsset objects cannot be null");
            }

            if (x.Platform == y.Platform)
            {
                if (x.Arch != y.Arch)
                {
                    if (x.Arch == CurrentArch)
                        return -1;
                    if (y.Arch == CurrentArch)
                        return 1;
                }
            }
            else
            {
                if (x.Platform == CurrentPlatform)
                    return -1;
                if (y.Platform == CurrentPlatform)
                    return 1;
            }

            // Prefer AppImage
            if (x.IsAppImage && !y.IsAppImage)
                return -1;
            if (!x.IsAppImage && y.IsAppImage)
                return 1;

            return 0;
        }

        private static OSPlatform GetCurrentOS()
        {
            return true switch
            {
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => OSPlatform.Windows,
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => OSPlatform.Linux,
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => OSPlatform.OSX,
                _ => throw new NotImplementedException()
            };
        }
    }
}
