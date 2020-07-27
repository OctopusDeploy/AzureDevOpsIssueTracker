using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    class AzureDevOpsIssueTracker : IIssueTracker
    {
        internal static string Name = "Azure DevOps";

        readonly IAzureDevOpsConfigurationStore configurationStore;

        public AzureDevOpsIssueTracker(IAzureDevOpsConfigurationStore configurationStore)
        {
            this.configurationStore = configurationStore;
        }

        public string CommentParser => AzureDevOpsConfigurationStore.CommentParser;
        public string IssueTrackerName => Name;

        public bool IsEnabled => configurationStore.GetIsEnabled();

        public string? BaseUrl => configurationStore.GetIsEnabled() ? configurationStore.GetBaseUrl() : null;
    }
}