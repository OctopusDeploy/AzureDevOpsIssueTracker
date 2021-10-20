using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Octopus.Data;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Resources.Configuration;
using Octopus.Server.Extensibility.Web.Extensions;
using Swashbuckle.AspNetCore.Annotations;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Web
{
    class AzureDevOpsConnectivityCheckAction : SystemScopedApiController
    {
        private readonly IAzureDevOpsConfigurationStore configurationStore;
        private readonly IAdoApiClient adoApiClient;

        public AzureDevOpsConnectivityCheckAction(IAzureDevOpsConfigurationStore configurationStore, IAdoApiClient adoApiClient)
        {
            this.configurationStore = configurationStore;
            this.adoApiClient = adoApiClient;
        }

        [SwaggerOperation(
            Summary = "Checks the AzureDevOps connection.",
            OperationId = "createAzureDevOpsConnectivityCheck"),
        ]
        [HttpPost(AzureDevOpsIssueTrackerApi.ApiConnectivityCheck)]
        public async Task<ConnectivityCheckResponse> Execute([FromBody]
            [SwaggerRequestBody("The connection check data")] ConnectionCheckData requestData, CancellationToken cancellationToken)
        {
            var connectivityCheckResponse = new ConnectivityCheckResponse();

            try
            {
                var baseUrl = requestData.BaseUrl;
                // If PersonalAccessToken here is null, it could be that they're clicking the test connectivity button after saving
                // the configuration as we won't have the value of the PersonalAccessToken on client side, so we need to retrieve it
                // from the database
                var personalAccessToken = requestData.PersonalAccessToken.ToSensitiveString();
                if (string.IsNullOrEmpty(personalAccessToken?.Value))
                {
                    personalAccessToken = await configurationStore.GetPersonalAccessToken(cancellationToken);
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, "Please provide a value for Azure DevOps Base Url.");
                    return connectivityCheckResponse;
                }

                var urls = AdoProjectUrls.ParseOrganizationAndProjectUrls(baseUrl);
                AdoProjectUrls[] projectUrls;
                if (urls.ProjectUrl != null)
                {
                    projectUrls = new[] {urls};
                }
                else
                {
                    var projectsResult = await adoApiClient.GetProjectList(urls, personalAccessToken?.Value, true, cancellationToken);
                    if (projectsResult is FailureResult failure)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, failure.ErrorString);
                        return connectivityCheckResponse;
                    }

                    var projects = (ISuccessResult<string[]>) projectsResult;

                    if (!projects.Value.Any())
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, "Successfully connected, but unable to find any projects to test permissions.");
                        return connectivityCheckResponse;
                    }

                    projectUrls = projects.Value.Select(project => new AdoProjectUrls(urls.OrganizationUrl)
                    {
                        ProjectUrl = $"{urls.OrganizationUrl}/{project}"
                    }).ToArray();
                }

                foreach (var projectUrl in projectUrls)
                {
                    var buildScopeTest = await adoApiClient.GetBuildWorkItemsRefs(AdoBuildUrls.Create(projectUrl, 1), personalAccessToken?.Value, true, cancellationToken);
                    if (buildScopeTest is FailureResult buildScopeFailure)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Warning, buildScopeFailure.ErrorString);
                        continue;
                    }

                    var workItemScopeTest = await adoApiClient.GetWorkItem(projectUrl, 1, personalAccessToken?.Value, true, cancellationToken);
                    if (workItemScopeTest is FailureResult workItemScopeFailure)
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Warning, workItemScopeFailure.ErrorString);
                        continue;
                    }

                    // the check has been successful, so ignore any messages that came from previous project checks
                    connectivityCheckResponse = new ConnectivityCheckResponse();
                    connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Info, "The Azure DevOps connection was tested successfully");
                    if (!await configurationStore.GetIsEnabled(cancellationToken))
                    {
                        connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Info, "The Jira Issue Tracker is not enabled, so its functionality will not currently be available");
                    }

                    return connectivityCheckResponse;
                }

                return connectivityCheckResponse;
            }
            catch (Exception ex)
            {
                connectivityCheckResponse.AddMessage(ConnectivityCheckMessageCategory.Error, ex.ToString());
                return connectivityCheckResponse;
            }
        }
    }

#nullable disable
    class ConnectionCheckData
    {
        public string BaseUrl { get; set; }
        public string PersonalAccessToken { get; set; }
    }
}
