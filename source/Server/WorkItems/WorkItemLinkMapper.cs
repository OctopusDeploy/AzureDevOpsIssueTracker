﻿using System;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Resources.IssueTrackers;
using Octopus.Versioning;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems
{
    public class WorkItemLinkMapper : IWorkItemLinkMapper
    {
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly IAdoApiClient client;

        public WorkItemLinkMapper(IAzureDevOpsConfigurationStore store, IAdoApiClient client)
        {
            this.store = store;
            this.client = client;
        }

        public string CommentParser => AzureDevOpsConfigurationStore.CommentParser;
        public bool IsEnabled => store.GetIsEnabled();

        public SuccessOrErrorResult<WorkItemLink[]> Map(string packageId, IVersion version, OctopusBuildInformation buildInformation, ILogWithContext log)
        {
            if (!IsEnabled)
            {
                log.Verbose("Azure DevOps Issue Tracker is disabled in Settings.");
                return null;
            }

            if (buildInformation == null)
            {
                log.Info($"No build information was found for package {packageId} {version}. To incorporate build information, and enable support for work"
                         + $" items and release notes generation, consider adding a Push Build Information step to your build process.");
                return null;
            }

            if (buildInformation.BuildEnvironment != "Azure DevOps")
            {
                // We are only interested in build URLs from Azure DevOps, because get use its build APIs to get associated work items
                log.Verbose($"The build environment for package {packageId} {version} was '{buildInformation.BuildEnvironment}' rather than 'Azure DevOps',"
                            + $" so the build URL will not be checked for Azure DevOps work item associations.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(buildInformation.BuildUrl))
            {
                log.Info($"No build URL was found in the build information for package {packageId} {version}, so it will not be checked for Azure"
                         + $" DevOps work item associations.");
                return null;
            }

            try
            {
                return client.GetBuildWorkItemLinks(AdoBuildUrls.ParseBrowserUrl(buildInformation.BuildUrl));
            }
            catch (Exception ex)
            {
                return SuccessOrErrorResult.Failure(ex.Message);
            }
        }
    }
}