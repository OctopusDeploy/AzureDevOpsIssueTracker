using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Resources.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Web
{
    class AzureDevOpsConnectivityCheckAction : IAsyncApiAction
    {
        private readonly IAzureDevOpsConfigurationStore configurationStore;
        private readonly IAdoApiClient adoApiClient;

        public AzureDevOpsConnectivityCheckAction(IAzureDevOpsConfigurationStore configurationStore, IAdoApiClient adoApiClient)
        {
            this.configurationStore = configurationStore;
            this.adoApiClient = adoApiClient;
        }

        public async Task<OctoResponse> ExecuteAsync(IOctoRequest request)
        {
            var connectivityCheckResponse = new ConnectivityCheckResponse();

            try
            {
                var requestData = request.GetBody<JObject>();

                var baseUrl = requestData.GetValue("BaseUrl").ToString();
                // If PersonalAccessToken here is null, it could be that they're clicking the test connectivity button after saving
                // the configuration as we won't have the value of the PersonalAccessToken on client side, so we need to retrieve it
                // from the database
                var personalAccessToken = requestData.GetValue("PersonalAccessToken").ToString().ToSensitiveString();
                if (string.IsNullOrEmpty(personalAccessToken.Value))
                {
                    personalAccessToken = configurationStore.GetPersonalAccessToken();
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, "Please provide a value for Azure DevOps Base Url.");
                    return new OctoDataResponse(connectivityCheckResponse);
                }

                var urls = AdoProjectUrls.ParseOrganizationAndProjectUrls(baseUrl);
                AdoProjectUrls[] projectUrls;
                if (urls.ProjectUrl != null)
                {
                    projectUrls = new[] {urls};
                }
                else
                {
                    var projects = adoApiClient.GetProjectList(urls, personalAccessToken.Value, true);
                    if (!projects.Succeeded)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, projects.FailureReason);
                        return new OctoDataResponse(connectivityCheckResponse);
                    }

                    if (!projects.Value.Any())
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, "Successfully connected, but unable to find any projects to test permissions.");
                        return new OctoDataResponse(connectivityCheckResponse);
                    }

                    projectUrls = projects.Value.Select(project => new AdoProjectUrls
                    {
                        OrganizationUrl = urls.OrganizationUrl,
                        ProjectUrl = $"{urls.OrganizationUrl}/{project}"
                    }).ToArray();
                }

                foreach (var projectUrl in projectUrls)
                {
                    var buildScopeTest = adoApiClient.GetBuildWorkItemsRefs(AdoBuildUrls.Create(projectUrl, 1), personalAccessToken.Value, true);
                    if (!buildScopeTest.Succeeded)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Warning, buildScopeTest.FailureReason);
                        continue;
                    }

                    var workItemScopeTest = adoApiClient.GetWorkItem(projectUrl, 1, personalAccessToken.Value, true);
                    if (!workItemScopeTest.Succeeded)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Warning, workItemScopeTest.FailureReason);
                        continue;
                    }

                    // the check has been successful, so ignore any messages that came from previous project checks
                    connectivityCheckResponse = new ConnectivityCheckResponse();
                    connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Info, "The Azure DevOps connection was tested successfully");
                    if (!configurationStore.GetIsEnabled())
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Info, "The Jira Issue Tracker is not enabled, so its functionality will not currently be available");
                    }

                    return new OctoDataResponse(connectivityCheckResponse);
                }

                return new OctoDataResponse(connectivityCheckResponse);
            }
            catch (Exception ex)
            {
                connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, ex.ToString());
                return new OctoDataResponse(connectivityCheckResponse);
            }
        }
    }
}