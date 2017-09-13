// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Extensions
{
    public static class TaskExtensions
    {
        public static Task WhenAll(this IEnumerable<Task> collection)
        {
            return Task.WhenAll(collection);
        }

        public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> collection)
        {
            return Task.WhenAll(collection);
        }
    }
}