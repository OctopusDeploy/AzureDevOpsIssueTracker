﻿using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.HostServices.Model.IssueTrackers;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Resources.IssueTrackers;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class WorkItemLinkMapperScenarios
    {
        private WorkItemLinkMapper CreateWorkItemLinkMapper(bool enabled, Func<SuccessOrErrorResult<WorkItemLink[]>> callback = null)
        {
            var config = Substitute.For<IAzureDevOpsConfigurationStore>();
            config.GetIsEnabled().Returns(enabled);
            var adoApiClient = Substitute.For<IAdoApiClient>();
            adoApiClient.GetBuildWorkItemLinks(null).ReturnsForAnyArgs(ci => callback?.Invoke() ?? throw new InvalidOperationException());
            return new WorkItemLinkMapper(config, adoApiClient);
        }

        [Test]
        public void WhenDisabledReturnsNull()
        {
            var links = CreateWorkItemLinkMapper(false).Map(new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            });
            Assert.IsTrue(links.Succeeded);
            Assert.IsNull(links.Value);
        }
    }
}