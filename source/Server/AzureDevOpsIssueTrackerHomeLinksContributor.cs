using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Web;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    internal class AzureDevOpsIssueTrackerHomeLinksContributor : IHomeLinksContributor
    {
        public IDictionary<string, string> GetLinksToContribute()
        {
            return new Dictionary<string, string> { { "AzureDevOpsConnectivityCheck", $"~{AzureDevOpsIssueTrackerApi.ApiConnectivityCheck}" } };
        }
    }
}