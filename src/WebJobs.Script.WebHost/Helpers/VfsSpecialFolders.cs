// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Script.WebHost.Management;
using Microsoft.Azure.WebJobs.Script.WebHost.Models;
using WebJobs.Script.WebHost.Extensions;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Helpers
{
    public static class VfsSpecialFolders
    {
        private const string SystemDriveFolder = "SystemDrive";
        private const string LocalSiteRootFolder = "LocalSiteRoot";

        private static string _systemDrivePath;
        private static string _localSiteRootPath;

        public static string SystemDrivePath
        {
            get
            {
                if (_systemDrivePath == null)
                {
                    _systemDrivePath = Environment.GetEnvironmentVariable(SystemDriveFolder) ?? string.Empty;
                }

                return _systemDrivePath;
            }

            // internal for testing purpose
            internal set
            {
                _systemDrivePath = value;
            }
        }

        public static string LocalSiteRootPath
        {
            get
            {
                if (_localSiteRootPath == null)
                {
                    // only light up in Azure env
                    string tmpPath = Environment.GetEnvironmentVariable("TMP");
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) &&
                        !string.IsNullOrEmpty(tmpPath))
                    {
                        _localSiteRootPath = Path.GetDirectoryName(tmpPath);
                    }
                }

                return _localSiteRootPath;
            }

            // internal for testing purpose
            internal set
            {
                _localSiteRootPath = value;
            }
        }

        public static IEnumerable<VfsStatEntry> GetEntries(string baseAddress, string query)
        {
            if (!string.IsNullOrEmpty(SystemDrivePath))
            {
                var dir = FileSystemHelpers.DirectoryInfoFromDirectoryName(SystemDrivePath + Path.DirectorySeparatorChar);
                yield return new VfsStatEntry
                {
                    Name = SystemDriveFolder,
                    MTime = dir.LastWriteTimeUtc,
                    CRTime = dir.CreationTimeUtc,
                    Mime = "inode/shortcut",
                    Href = baseAddress + Uri.EscapeUriString(SystemDriveFolder + VirtualFileSystemBase.UriSegmentSeparator) + query,
                    Path = dir.FullName
                };
            }

            if (!string.IsNullOrEmpty(LocalSiteRootPath))
            {
                var dir = FileSystemHelpers.DirectoryInfoFromDirectoryName(LocalSiteRootPath);
                yield return new VfsStatEntry
                {
                    Name = LocalSiteRootFolder,
                    MTime = dir.LastWriteTimeUtc,
                    CRTime = dir.CreationTimeUtc,
                    Mime = "inode/shortcut",
                    Href = baseAddress + Uri.EscapeUriString(LocalSiteRootFolder + VirtualFileSystemBase.UriSegmentSeparator) + query,
                    Path = dir.FullName
                };
            }
        }

        public static bool TryHandleRequest(HttpRequest request, string path, out HttpResponseMessage response)
        {
            response = null;
            if (string.Equals(path, SystemDrivePath, StringComparison.OrdinalIgnoreCase))
            {
                response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
                UriBuilder location = new UriBuilder(request.GetRequestUri());
                location.Path += "/";
                response.Headers.Location = location.Uri;
            }

            return response != null;
        }

        // this resolves the special folders such as SystemDrive or LocalSiteRoot
        public static bool TryParse(string path, out string result)
        {
            result = null;
            if (!string.IsNullOrEmpty(path))
            {
                if (string.Equals(path, SystemDriveFolder, StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf(SystemDriveFolder + VirtualFileSystemBase.UriSegmentSeparator, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (!string.IsNullOrEmpty(SystemDrivePath))
                    {
                        string relativePath = path.Substring(SystemDriveFolder.Length);
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            result = SystemDrivePath;
                        }
                        else
                        {
                            result = Path.GetFullPath(SystemDrivePath + relativePath);
                        }
                    }
                }
                else if (string.Equals(path, LocalSiteRootFolder, StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf(LocalSiteRootFolder + VirtualFileSystemBase.UriSegmentSeparator, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (!string.IsNullOrEmpty(LocalSiteRootPath))
                    {
                        string relativePath = path.Substring(LocalSiteRootFolder.Length);
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            result = LocalSiteRootPath;
                        }
                        else
                        {
                            result = Path.GetFullPath(LocalSiteRootPath + relativePath);
                        }
                    }
                }
            }

            return result != null;
        }
    }
}