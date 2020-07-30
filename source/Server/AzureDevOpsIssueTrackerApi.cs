using System;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Web;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    class AzureDevOpsIssueTrackerApi : RegistersEndpoints
    {
        public const string ApiConnectivityCheck = "/azuredevopsissuetracker/connectivitycheck";

        public AzureDevOpsIssueTrackerApi()
        {
            Add<AzureDevOpsConnectivityCheckAction>("POST", ApiConnectivityCheck, new SecuredEndpointInvocation());
        }
    }
}