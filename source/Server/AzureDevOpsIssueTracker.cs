using System.Threading;
using System.Threading.Tasks;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    class AzureDevOpsIssueTracker : IIssueTracker
    {
        static string Name = "Azure DevOps";

        readonly IAzureDevOpsConfigurationStore configurationStore;

        public AzureDevOpsIssueTracker(IAzureDevOpsConfigurationStore configurationStore)
        {
            this.configurationStore = configurationStore;
        }

        public string CommentParser => AzureDevOpsConfigurationStore.CommentParser;
        public async Task<bool> IsEnabled(CancellationToken cancellationToken)
        {
            return await configurationStore.GetIsEnabled(cancellationToken);
        }

        public async Task<string?> BaseUrl(CancellationToken cancellationToken)
        {
            if (await configurationStore.GetIsEnabled(cancellationToken))
            {
                return await configurationStore.GetBaseUrl(cancellationToken);
            }

            return null;
        }

        public string IssueTrackerName => Name;
    }
}