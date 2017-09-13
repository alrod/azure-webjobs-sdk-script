using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace WebJobs.Script.Management.Models
{
    public class FunctionMetadataResponse
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "script_root_path_href")]
        public Uri ScriptRootPathHref { get; set; }

        [JsonProperty(PropertyName = "script_href")]
        public Uri ScriptHref { get; set; }

        [JsonProperty(PropertyName = "config_href")]
        public Uri ConfigHref { get; set; }

        [JsonProperty(PropertyName = "href")]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "config")]
        public JObject Config { get; set; }

        [JsonProperty(PropertyName = "files")]
        public IDictionary<string, string> Files { get; set; }

        [JsonProperty(PropertyName = "test_data")]
        public string TestData { get; set; }

        [JsonProperty(PropertyName = "isDisabled")]
        public bool IsDisabled { get; set; }

        [JsonProperty(PropertyName = "isExcluded")]
        public bool IsExcluded { get; set; }

        [JsonProperty(PropertyName = "isDirect")]
        public bool IsDirect { get; set; }

        [JsonProperty(PropertyName = "isProxy")]
        public bool IsProxy { get; set; }
    }
}