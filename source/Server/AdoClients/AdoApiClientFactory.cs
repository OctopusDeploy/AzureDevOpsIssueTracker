using Octopus.Diagnostics;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    public interface IAdoApiClientFactory
    {
        IAdoApiClient CreateWithLog(ILogWithContext log);
    }

    public class AdoApiClientFactory : IAdoApiClientFactory
    {
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly HtmlConvert htmlConvert;

        public AdoApiClientFactory(IAzureDevOpsConfigurationStore store, HtmlConvert htmlConvert)
        {
            this.store = store;
            this.htmlConvert = htmlConvert;
        }

        public IAdoApiClient CreateWithLog(ILogWithContext log)
        {
            return new AdoApiClient(store, new HttpJsonClient(log), htmlConvert, log);
        }
    }
}