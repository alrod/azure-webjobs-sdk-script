// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Helpers
{
    public static class FileSystemHelpers
    {
        public static void DeleteDirectoryContentsSafe(string path, bool ignoreErrors = true)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi, ignoreErrors);
                    }
                }
            }
            catch when (ignoreErrors)
            {
            }
        }

        public static async Task WriteAllTextToFile(string path, string content)
        {
            using (var fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (var streamWriter = new StreamWriter(fileStream))
            {
                await streamWriter.WriteAsync(content);
                await streamWriter.FlushAsync();
            }
        }

        public static async Task<string> ReadAllTextFromFile(string path)
        {
            using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var streamReader = new StreamReader(fileStream))
            {
                return await streamReader.ReadToEndAsync();
            }
        }

        public static bool FileExists(string path) => File.Exists(path);

        public static bool DirectoryExists(string path) => Directory.Exists(path);

        private static void DeleteFileSystemInfo(FileSystemInfo fileSystemInfo, bool ignoreErrors)
        {
            if (!fileSystemInfo.Exists)
            {
                return;
            }

            try
            {
                fileSystemInfo.Attributes = FileAttributes.Normal;
            }
            catch when (ignoreErrors)
            {
            }

            if (fileSystemInfo is DirectoryInfo directoryInfo)
            {
                DeleteDirectoryContentsSafe(directoryInfo, ignoreErrors);
            }

            DoSafeAction(fileSystemInfo.Delete, ignoreErrors);
        }

        private static void DeleteDirectoryContentsSafe(DirectoryInfo directoryInfo, bool ignoreErrors)
        {
            try
            {
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi, ignoreErrors);
                    }
                }
            }
            catch when (ignoreErrors)
            {
            }
        }

        private static void DoSafeAction(Action action, bool ignoreErrors)
        {
            try
            {
                action();
            }
            catch when (ignoreErrors)
            {
            }
        }

        internal static DirectoryInfo DirectoryInfoFromDirectoryName(string localSiteRootPath) => new DirectoryInfo(localSiteRootPath);

        internal static FileInfo FileInfoFromFileName(string localFilePath) => new FileInfo(localFilePath);
    }
}