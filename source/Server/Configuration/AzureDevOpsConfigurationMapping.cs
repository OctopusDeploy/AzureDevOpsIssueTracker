using System;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    internal class AzureDevOpsConfigurationMapping : IConfigurationDocumentMapper
    {
        public Type GetTypeToMap()
        {
            return typeof(AzureDevOpsConfiguration);
        }
    }
}