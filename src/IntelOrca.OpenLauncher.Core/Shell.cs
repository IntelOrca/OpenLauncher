using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace IntelOrca.OpenLauncher.Core
{
    public class Shell
    {
        public void StartProcess(string name, params string[] args)
        {
            var psi = new ProcessStartInfo(name);
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            Process.Start(psi);
        }

        public int RunProcess(string name, params string[] args)
        {
            var psi = new ProcessStartInfo(name);
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            var p = Process.Start(psi);
            p.WaitForExit();
            return p.ExitCode;
        }

        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public string[] GetFileSystemEntries(string path) => Directory.GetFileSystemEntries(path);

        public void MoveDirectory(string src, string dst) => Directory.Move(src, dst);

        public void MoveFile(string src, string dst) => File.Move(src, dst);

        public void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void TryDeleteFile(string path)
        {
            try
            {
                DeleteFile(path);
            }
            catch
            {
            }
        }

        public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);

        public void SetExecutable(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var exitCode = RunProcess("chmod", "+x", path);
                if (exitCode != 0)
                {
                    throw new Exception($"Failed to run chmod on '{path}'");
                }
            }
        }
    }
}
