using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Data.Model;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigureCommands : IContributeToConfigureCommand
    {
        readonly ISystemLog systemLog;
        readonly Lazy<IAzureDevOpsConfigurationStore> azureDevOpsConfiguration;

        public AzureDevOpsConfigureCommands(
            ISystemLog systemLog,
            Lazy<IAzureDevOpsConfigurationStore> azureDevOpsConfiguration)
        {
            this.systemLog = systemLog;
            this.azureDevOpsConfiguration = azureDevOpsConfiguration;
        }

        public IEnumerable<ConfigureCommandOption> GetOptions()
        {
            yield return new ConfigureCommandOption("AzureDevOpsIsEnabled=", "Set whether Azure DevOps issue tracker integration is enabled.",
                v =>
                {
                    var isEnabled = bool.Parse(v);
                    azureDevOpsConfiguration.Value.SetIsEnabled(isEnabled, CancellationToken.None);
                    systemLog.Info($"Azure DevOps Issue Tracker integration IsEnabled set to: {isEnabled}");
                });
            yield return new ConfigureCommandOption("AzureDevOpsBaseUrl=", AzureDevOpsConfigurationResource.BaseUrlDescription,
                v =>
                {
                    azureDevOpsConfiguration.Value.SetBaseUrl(v, CancellationToken.None);
                    systemLog.Info($"Azure DevOps Issue Tracker integration base Url set to: {v}");
                });
            yield return new ConfigureCommandOption("AzureDevOpsPersonalAccessToken=", AzureDevOpsConfigurationResource.PersonalAccessTokenDescription,
                v =>
                {
                    azureDevOpsConfiguration.Value.SetPersonalAccessToken(v.ToSensitiveString(), CancellationToken.None);
                    systemLog.Info($"Azure DevOps Issue Tracker integration personal access token set to: {v}");
                });
        }
    }
}