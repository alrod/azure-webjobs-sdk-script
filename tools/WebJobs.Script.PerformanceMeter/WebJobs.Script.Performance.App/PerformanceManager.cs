using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using WebJobs.Script.Tests.EndToEnd.Shared;

namespace WebJobs.Script.PerformanceMeter
{
    class PerformanceManager : IDisposable
    {
        private readonly FunctionAppFixture _fixture;
        private readonly ComputeManagementClient _client;
        private bool _disposed = false;

        public PerformanceManager(string runtimeExtensionPackageUrl)
        {
            _fixture = new FunctionAppFixture(runtimeExtensionPackageUrl);

            var authenticationContext = new AuthenticationContext($"https://login.windows.net/{Settings.SiteTenantId}");
            var credential = new ClientCredential(Settings.SiteApplicationId, Settings.SiteClientSecret);
            var result = authenticationContext.AcquireTokenAsync("https://management.core.windows.net/", credential);

            result.Wait();
            if (result.Result == null)
                throw new AuthenticationException("Failed to obtain the JWT token");

            var credentials = new TokenCredentials(result.Result.AccessToken);
            _client = new ComputeManagementClient(credentials);
            _client.SubscriptionId = Settings.SiteSubscriptionId;

        }

        public async Task ExecuteAsync(string testId)
        {
            // We are assume first word in testId is paltform
            string platform = testId.Split("-")[0];
            string description = "Desc";
            await ChangeLanguage(platform);

            _fixture.Logger.LogInformation($"Execute: {testId}, {description}");

            var commandResult = await VirtualMachinesOperationsExtensions.RunCommandAsync(_client.VirtualMachines, Settings.SiteResourceGroup, Settings.VM,
                new RunCommandInput("RunPowerShellScript",
                new List<string>() { $"& 'C:\\Tools\\ps\\test-throughput.ps1' '{testId}' '{description}' '{Settings.RuntimeVersion}'" }));

        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                _client.Dispose();
                _disposed = true;
            }
        }

        public async Task ExecuteAllAsync()
        {
            var commandResult = await VirtualMachinesOperationsExtensions.RunCommandAsync(_client.VirtualMachines, Settings.SiteResourceGroup, Settings.VM,
                new RunCommandInput("RunPowerShellScript",
                new List<string>() { $"& 'C:\\Tools\\ps\\get-all-tests.ps1'" }));
        }

        private async Task ChangeLanguage(string language)
        {
            _fixture.Logger.LogInformation($"Changing language: {language}");
            await _fixture.AddAppSetting("FUNCTIONS_WORKER_RUNTIME", language);

            // Wait until the app fully restaeted and ready
            Thread.Sleep(30000);
            await _fixture.KuduClient.GetFunctions();
        }
    }
}
