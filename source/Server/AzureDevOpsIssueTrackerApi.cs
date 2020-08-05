using System;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Web;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    class AzureDevOpsIssueTrackerApi : RegistersEndpoints
    {
        public const string ApiConnectivityCheck = "/api/azuredevopsissuetracker/connectivitycheck";

        public AzureDevOpsIssueTrackerApi()
        {
            Add<AzureDevOpsConnectivityCheckAction>("POST", ApiConnectivityCheck, RouteCategory.Raw, new SecuredEndpointInvocation(), null, "AzureDevOps");
        }
    }
}