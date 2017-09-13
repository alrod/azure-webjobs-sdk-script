// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script;
using Microsoft.Azure.WebJobs.Script.Description;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using WebJobs.Script.Management.Models;
using WebJobs.Script.Management.Mappers;
using Microsoft.Azure.WebJobs.Script.Config;

namespace WebJobs.Script.Management
{
    public class ValueMapper
    {
        public ScriptHostConfiguration _config { get; }
        public ScriptSettingsManager _settings { get; }

        public ValueMapper(ScriptHostConfiguration config, ScriptSettingsManager settings)
        {
            _config = config;
            _settings = settings;
            SetupMaps();
        }
       
        private void SetupMaps()
        {
            // investigate using ApiModelUtility instead.
            // the problem is that the payload looks very different.
            AddMap<FunctionMetadata, FunctionMetadataResponse>((functionMetadata, request) =>
                FunctionMetadataMapper.Map(functionMetadata, request, _config, _settings));

        }

        public void AddMap<T, U>(Func<T, HttpRequest, Task<U>> func)
        {
            GenericHelper<T, U>.map.AddOrUpdate(GetKey<U, T>(), func, (_, __) => func);
        }

        public async Task<U> Map<T, U>(T source, HttpRequest request)
        {
            if (GenericHelper<T, U>.map.TryGetValue(GetKey<T, U>(), out Func<T, HttpRequest, Task<U>> func))
            {
                return await func(source, request);
            }
            else
            {
                return default(U);
            }
        }

        private string GetKey<T, U>() => $"{typeof(T).FullName} -> {typeof(U).FullName}";

        private static class GenericHelper<T, U>
        {
            public static ConcurrentDictionary<string, Func<T, HttpRequest, Task<U>>> map = new ConcurrentDictionary<string, Func<T, HttpRequest, Task<U>>>();
        }
    }
}
