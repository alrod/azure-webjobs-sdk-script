using Microsoft.Azure.WebJobs.Script.Description;
using System;
using System.Collections.Generic;
using System.Text;
using WebJobs.Script.Management.Contracts;
using System.Threading.Tasks;
using WebJobs.Script.Management.Models;
using Microsoft.Azure.WebJobs.Script;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace WebJobs.Script.Management
{
    public class WebFunctionsManager : IWebFunctionsManager
    {
        private readonly ScriptHostConfiguration _config;
        private readonly ScriptSettingsManager _settings;
        private readonly TraceWriter _traceWriter;
        private readonly ILogger _logger;
        private readonly ValueMapper _valueMapper;

        public WebFunctionsManager(ScriptHostConfiguration config, ScriptSettingsManager settings, TraceWriter traceWriter, ILoggerFactory loggerFactory, ValueMapper valueMapper)
        {
            _config = config;
            _settings = settings;
            _traceWriter = traceWriter;
            _logger = loggerFactory?.CreateLogger(ScriptConstants.LogCategoryKeysController);
            _valueMapper = valueMapper;
        }

        public async Task<IEnumerable<FunctionMetadataResponse>> GetFunctionsMetadata(HttpRequest request)
        {
            return await Task.WhenAll(
                ScriptHost.ReadFunctionMetadata(_config, _traceWriter, _logger, new Dictionary<string, Collection<string>>(), _settings)
                .Select(fm => _valueMapper.Map<FunctionMetadata, FunctionMetadataResponse>(fm, request)));

        }
    }
}
