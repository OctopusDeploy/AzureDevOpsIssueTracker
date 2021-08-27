using Octopus.Data.Storage.Configuration;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions.Infrastructure;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    internal class DatabaseInitializer : ExecuteWhenDatabaseInitializes
    {
        private readonly IConfigurationStore configurationStore;
        private readonly ISystemLog systemLog;

        public DatabaseInitializer(ISystemLog systemLog, IConfigurationStore configurationStore)
        {
            this.systemLog = systemLog;
            this.configurationStore = configurationStore;
        }

        public override void Execute()
        {
            var doc = configurationStore.Get<AzureDevOpsConfiguration>(AzureDevOpsConfigurationStore.SingletonId);
            if (doc != null)
                return;

            systemLog.Info("Initializing Azure DevOps integration settings");
            doc = new AzureDevOpsConfiguration();
            configurationStore.Create(doc);
        }
    }
}