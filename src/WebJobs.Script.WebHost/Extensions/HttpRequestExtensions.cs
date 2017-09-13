// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace WebJobs.Script.WebHost.Extensions
{
    public static class HttpRequestExtensions
    {
        private const string GeoLocationHeaderKey = "x-ms-geo-location";

        public static bool IsArmRequest(this HttpRequest request) =>
            request.Headers != null &&
            request.Headers.ContainsKey(GeoLocationHeaderKey);

        public static Uri GetRequestUri(this HttpRequest request) => new Uri(request.GetDisplayUrl());
    }
}