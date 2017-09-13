// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WebJobs.Script.Management.Models;

namespace WebJobs.Script.WebHost.Management
{
    public class WebFunctionsManager : IWebFunctionsManager
    {
        private readonly ScriptHostConfiguration _config;
        private readonly ILogger _logger;

        public WebFunctionsManager(WebHostSettings webSettings, ILoggerFactory loggerFactory)
        {
            _config = WebHostResolver.CreateScriptHostConfiguration(webSettings);
            _logger = loggerFactory?.CreateLogger(ScriptConstants.LogCategoryKeysController);
        }

        public async Task<IEnumerable<FunctionMetadataResponse>> GetFunctionsMetadata(HttpRequest request)
        {
            return await ScriptHost.ReadFunctionsMetadata(_config, NullTraceWriter.Instance, _logger, new Dictionary<string, Collection<string>>())
                .Select(fm => fm.ToFunctionMetadataResponse(request, _config))
                .WhenAll();
        }

        public async Task<(bool, bool, FunctionMetadataResponse)> CreateOrUpdate(string name, FunctionMetadataResponse functionMetadata, HttpRequest request)
        {
            var configChanged = false;
            var functionDir = Path.Combine(_config.RootScriptPath, name);

            // Make sure the function folder exists
            if (!FileSystemHelpers.DirectoryExists(functionDir))
            {
                // Cleanup any leftover artifacts from a function with the same name before.
                DeleteFunctionArtifacts(name);
                Directory.CreateDirectory(functionDir);
            }

            string newConfig = null;
            string configPath = Path.Combine(functionDir, ScriptConstants.FunctionMetadataFileName);
            string dataFilePath = FunctionMetadataExtensions.GetTestDataFilePath(name, _config);

            // If files are included, write them out
            if (functionMetadata?.Files != null)
            {
                // If the config is passed in the file collection, save it and don't process it as a file
                if (functionMetadata.Files.TryGetValue(ScriptConstants.FunctionMetadataFileName, out newConfig))
                {
                    functionMetadata.Files.Remove(ScriptConstants.FunctionMetadataFileName);
                }

                // Delete all existing files in the directory. This will also delete current function.json, but it gets recreated below
                FileSystemHelpers.DeleteDirectoryContentsSafe(functionDir);

                await functionMetadata
                    .Files
                    .Select(e => FileSystemHelpers.WriteAllTextToFile(Path.Combine(functionDir, e.Key), e.Value))
                    .WhenAll();
            }

            // Get the config (if it was not already passed in as a file)
            if (newConfig == null && functionMetadata?.Config != null)
            {
                newConfig = JsonConvert.SerializeObject(functionMetadata?.Config, Formatting.Indented);
            }

            // Get the current config, if any
            string currentConfig = null;
            if (FileSystemHelpers.FileExists(configPath))
            {
                currentConfig = await FileSystemHelpers.ReadAllTextFromFile(configPath);
            }

            // Save the file and set changed flag is it has changed. This helps optimize the syncTriggers call
            if (newConfig != currentConfig)
            {
                await FileSystemHelpers.WriteAllTextToFile(configPath, newConfig);
                configChanged = true;
            }

            if (functionMetadata.TestData != null)
            {
                await FileSystemHelpers.WriteAllTextToFile(dataFilePath, functionMetadata.TestData);
            }

            (var success, var functionMetadataResult) = await TryGetFunction(name, request); // test_data took from incoming request, it will not exceed the limit
            return (success, configChanged, functionMetadataResult);
        }

        public async Task<(bool, FunctionMetadataResponse)> TryGetFunction(string name, HttpRequest request)
        {
            var functionMetadata = ScriptHost.ReadFunctionMetadata(Path.Combine(_config.RootScriptPath, name), _config, NullTraceWriter.Instance, _logger, new Dictionary<string, Collection<string>>());
            if (functionMetadata != null)
            {
                return (true, await functionMetadata.ToFunctionMetadataResponse(request, _config));
            }
            else
            {
                return (false, null);
            }
        }

        private void DeleteFunctionArtifacts(string name)
        {
            // File.DeleteFileSafe(GetFunctionTestDataFilePath(name));
            // File.DeleteFileSafe(GetFunctionSecretsFilePath(name));
            // File.DeleteFileSafe(GetFunctionLogPath(name));
        }
    }
}