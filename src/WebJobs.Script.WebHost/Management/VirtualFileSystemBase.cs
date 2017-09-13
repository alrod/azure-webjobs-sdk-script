// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Helpers;
using Microsoft.Azure.WebJobs.Script.WebHost.Models;
using Newtonsoft.Json;
using WebJobs.Script.WebHost.Extensions;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Management
{
    public abstract class VirtualFileSystemBase
    {
        public const char UriSegmentSeparator = '/';
        private const string DirectoryEnumerationSearchPattern = "*";
        private const string DummyRazorExtension = ".func777";

        private static readonly char[] _uriSegmentSeparator = new char[] { UriSegmentSeparator };
        private static readonly MediaTypeHeaderValue _directoryMediaType = MediaTypeHeaderValue.Parse("inode/directory");

        protected const int BufferSize = 32 * 1024;
        private readonly ScriptHostConfiguration _config;

        protected VirtualFileSystemBase(WebHostSettings settings)
        {
            _config = settings.ToScriptHostConfiguration();
            MediaTypeMap = MediaTypeMap.Default;
        }

        protected string RootPath
        {
            get
            {
                return ScriptSettingsManager.Instance.GetSetting(EnvironmentSettingNames.AzureWebsiteHomePath)
                    ?? Path.GetFullPath(_config.RootScriptPath);
            }
        }

        protected MediaTypeMap MediaTypeMap { get; private set; }

        public virtual Task<HttpResponseMessage> GetItem(HttpRequest request)
        {
            string localFilePath = GetLocalFilePath(request);

            if (VfsSpecialFolders.TryHandleRequest(request, localFilePath, out HttpResponseMessage response))
            {
                return Task.FromResult(response);
            }

            var info = FileSystemHelpers.DirectoryInfoFromDirectoryName(localFilePath);

            if (info.Attributes < 0)
            {
                var notFoundResponse = CreateResponse(HttpStatusCode.NotFound, string.Format("'{0}' not found.", info.FullName));
                return Task.FromResult(notFoundResponse);
            }
            else if ((info.Attributes & FileAttributes.Directory) != 0)
            {
                // If request URI does NOT end in a "/" then redirect to one that does
                if (localFilePath[localFilePath.Length - 1] != Path.DirectorySeparatorChar)
                {
                    var redirectResponse = CreateResponse(HttpStatusCode.TemporaryRedirect);
                    var location = new UriBuilder(request.GetDisplayUrl());
                    location.Path += "/";
                    redirectResponse.Headers.Location = location.Uri;
                    return Task.FromResult(redirectResponse);
                }
                else
                {
                    return CreateDirectoryGetResponse(request, info, localFilePath);
                }
            }
            else
            {
                // If request URI ends in a "/" then redirect to one that does not
                if (localFilePath[localFilePath.Length - 1] == Path.DirectorySeparatorChar)
                {
                    HttpResponseMessage redirectResponse = CreateResponse(HttpStatusCode.TemporaryRedirect);
                    UriBuilder location = new UriBuilder(request.GetRequestUri());
                    location.Path = location.Path.TrimEnd(_uriSegmentSeparator);
                    redirectResponse.Headers.Location = location.Uri;
                    return Task.FromResult(redirectResponse);
                }

                // We are ready to get the file
                return CreateItemGetResponse(request, info, localFilePath);
            }
        }

        public virtual Task<HttpResponseMessage> PutItem(HttpRequest request)
        {
            var localFilePath = GetLocalFilePath(request);

            if (VfsSpecialFolders.TryHandleRequest(request, localFilePath, out HttpResponseMessage response))
            {
                return Task.FromResult(response);
            }

            var info = FileSystemHelpers.DirectoryInfoFromDirectoryName(localFilePath);
            var itemExists = info.Attributes >= 0;

            if (itemExists && (info.Attributes & FileAttributes.Directory) != 0)
            {
                return CreateDirectoryPutResponse(info, localFilePath);
            }
            else
            {
                // If request URI ends in a "/" then attempt to create the directory.
                if (localFilePath[localFilePath.Length - 1] == Path.DirectorySeparatorChar)
                {
                    return CreateDirectoryPutResponse(info, localFilePath);
                }

                // We are ready to update the file
                return CreateItemPutResponse(request, info, localFilePath, itemExists);
            }
        }

        public virtual Task<HttpResponseMessage> DeleteItem(HttpRequest request, bool recursive = false)
        {
            string localFilePath = GetLocalFilePath(request);

            if (VfsSpecialFolders.TryHandleRequest(request, localFilePath, out HttpResponseMessage response))
            {
                return Task.FromResult(response);
            }

            var dirInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(localFilePath);

            if (dirInfo.Attributes < 0)
            {
                var notFoundResponse = CreateResponse(HttpStatusCode.NotFound, string.Format("'{0}' not found.", dirInfo.FullName));
                return Task.FromResult(notFoundResponse);
            }
            else if ((dirInfo.Attributes & FileAttributes.Directory) != 0)
            {
                try
                {
                    dirInfo.Delete(recursive);
                }
                catch (Exception ex)
                {
                    // TODO: log ex
                    var conflictDirectoryResponse = CreateResponse(HttpStatusCode.Conflict, ex);
                    return Task.FromResult(conflictDirectoryResponse);
                }

                // Delete directory succeeded.
                var successResponse = CreateResponse(HttpStatusCode.OK);
                return Task.FromResult(successResponse);
            }
            else
            {
                // If request URI ends in a "/" then redirect to one that does not
                if (localFilePath[localFilePath.Length - 1] == Path.DirectorySeparatorChar)
                {
                    var redirectResponse = CreateResponse(HttpStatusCode.TemporaryRedirect);
                    var location = new UriBuilder(request.GetRequestUri());
                    location.Path = location.Path.TrimEnd(_uriSegmentSeparator);
                    redirectResponse.Headers.Location = location.Uri;
                    return Task.FromResult(redirectResponse);
                }

                // We are ready to delete the file
                var fileInfo = FileSystemHelpers.FileInfoFromFileName(localFilePath);
                return CreateFileDeleteResponse(request, fileInfo);
            }
        }

        protected virtual Task<HttpResponseMessage> CreateDirectoryGetResponse(HttpRequest request, DirectoryInfo info, string localFilePath)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            try
            {
                // Enumerate directory
                var directory = GetDirectoryResponse(request, info.GetFileSystemInfos());
                var successDirectoryResponse = CreateResponse(HttpStatusCode.OK, directory);
                return Task.FromResult(successDirectoryResponse);
            }
            catch (Exception e)
            {
                // TODO: log
                HttpResponseMessage errorResponse = CreateResponse(HttpStatusCode.InternalServerError, e.Message);
                return Task.FromResult(errorResponse);
            }
        }

        protected abstract Task<HttpResponseMessage> CreateItemGetResponse(HttpRequest request, FileSystemInfo info, string localFilePath);

        protected virtual Task<HttpResponseMessage> CreateDirectoryPutResponse(DirectoryInfo info, string localFilePath)
        {
            var conflictDirectoryResponse = CreateResponse(HttpStatusCode.Conflict);
            return Task.FromResult(conflictDirectoryResponse);
        }

        protected abstract Task<HttpResponseMessage> CreateItemPutResponse(HttpRequest request, FileSystemInfo info, string localFilePath, bool itemExists);

        protected virtual Task<HttpResponseMessage> CreateFileDeleteResponse(HttpRequest _, FileInfo info)
        {
            // Generate file response
            try
            {
                using (Stream fileStream = GetFileDeleteStream(info))
                {
                    info.Delete();
                }
                var successResponse = CreateResponse(HttpStatusCode.OK);
                return Task.FromResult(successResponse);
            }
            catch (Exception e)
            {
                // Could not delete the file
                // TODO: log ex
                var notFoundResponse = CreateResponse(HttpStatusCode.NotFound, e);
                return Task.FromResult(notFoundResponse);
            }
        }

        /// <summary>
        /// Indicates whether this is a conditional range request containing an
        /// If-Range header with a matching etag and a Range header indicating the
        /// desired ranges
        /// </summary>
        protected bool IsRangeRequest(HttpRequest request, EntityTagHeaderValue currentEtag)
        {
            var typedHeaders = request.GetTypedHeaders();
            if (typedHeaders.Range == null)
            {
                return false;
            }
            if (typedHeaders.IfRange != null)
            {
                return typedHeaders.IfRange.EntityTag.Equals(currentEtag);
            }
            return true;
        }

        /// <summary>
        /// Indicates whether this is a If-None-Match request with a matching etag.
        /// </summary>
        protected bool IsIfNoneMatchRequest(HttpRequest request, EntityTagHeaderValue currentEtag)
        {
            var typedHeaders = request.GetTypedHeaders();
            return currentEtag != null && typedHeaders.IfNoneMatch != null &&
                typedHeaders.IfNoneMatch.Any(entityTag => currentEtag.Equals(entityTag));
        }

        /// <summary>
        /// Provides a common way for opening a file stream for shared reading from a file.
        /// </summary>
        protected static Stream GetFileReadStream(string localFilePath)
        {
            if (localFilePath == null)
            {
                throw new ArgumentNullException(nameof(localFilePath));
            }

            // Open file exclusively for read-sharing
            return new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, useAsync: true);
        }

        /// <summary>
        /// Provides a common way for opening a file stream for writing exclusively to a file.
        /// </summary>
        protected static Stream GetFileWriteStream(string localFilePath, bool fileExists)
        {
            if (localFilePath == null)
            {
                throw new ArgumentNullException(nameof(localFilePath));
            }

            // Create path if item doesn't already exist
            if (!fileExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
            }

            // Open file exclusively for write without any sharing
            return new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        }

        /// <summary>
        /// Provides a common way for opening a file stream for exclusively deleting the file.
        /// </summary>
        private static Stream GetFileDeleteStream(FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            // Open file exclusively for delete sharing only
            return file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        // internal for testing purpose
        internal string GetLocalFilePath(HttpRequest request)
        {
            // Restore the original extension if we had added a dummy
            // See comment in TraceModule.OnBeginRequest
            string result = GetOriginalLocalFilePath(request);
            if (result.EndsWith(DummyRazorExtension, StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - DummyRazorExtension.Length);
            }

            return result;
        }

        private string GetOriginalLocalFilePath(HttpRequest request)
        {
            string result = null;
            if (request.Path.StartsWithSegments("/admin/vfs", out PathString path))
            {
                if (VfsSpecialFolders.TryParse(path, out result))
                {
                    return result;
                }
            }

            result = RootPath;
            if (path != null && path.HasValue)
            {
                result = Path.GetFullPath(Path.Combine(result, path.Value.TrimStart('/')));
            }
            else
            {
                string reqUri = request.GetRequestUri().AbsoluteUri.Split('?').First();
                if (reqUri[reqUri.Length - 1] == UriSegmentSeparator)
                {
                    result = Path.GetFullPath(result + Path.DirectorySeparatorChar);
                }
            }
            return result;
        }

        private IEnumerable<VfsStatEntry> GetDirectoryResponse(HttpRequest request, FileSystemInfo[] infos)
        {
            var uri = request.GetRequestUri();
            string baseAddress = uri.AbsoluteUri.Split('?').First();
            string query = uri.Query;
            foreach (FileSystemInfo fileSysInfo in infos)
            {
                bool isDirectory = (fileSysInfo.Attributes & FileAttributes.Directory) != 0;
                string mime = isDirectory ? _directoryMediaType.ToString() : MediaTypeMap.GetMediaType(fileSysInfo.Extension).ToString();
                string unescapedHref = isDirectory ? fileSysInfo.Name + UriSegmentSeparator : fileSysInfo.Name;
                long size = isDirectory ? 0 : ((FileInfo)fileSysInfo).Length;

                yield return new VfsStatEntry
                {
                    Name = fileSysInfo.Name,
                    MTime = fileSysInfo.LastWriteTimeUtc,
                    CRTime = fileSysInfo.CreationTimeUtc,
                    Mime = mime,
                    Size = size,
                    Href = (baseAddress + Uri.EscapeUriString(unescapedHref) + query).EscapeHashCharacter(),
                    Path = fileSysInfo.FullName
                };
            }

            // add special folders when requesting Root url
            var routeData = request.HttpContext.GetRouteData();
            if (routeData != null && string.IsNullOrEmpty(routeData.Values["path"] as string))
            {
                foreach (var entry in VfsSpecialFolders.GetEntries(baseAddress, query))
                {
                    yield return entry;
                }
            }
        }

        protected HttpResponseMessage CreateResponse(HttpStatusCode statusCode, object payload = null)
        {
            var response = new HttpResponseMessage(statusCode);
            if (payload != null)
            {
                var content = payload is string ? payload as string : JsonConvert.SerializeObject(payload);
                response.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }
            return response;
        }
    }
}