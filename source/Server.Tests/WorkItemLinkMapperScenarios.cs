using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.HostServices.Model.IssueTrackers;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Resources.IssueTrackers;
using Octopus.Versioning.Semver;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class WorkItemLinkMapperScenarios
    {
        private WorkItemLinkMapper CreateWorkItemLinkMapper(bool enabled)
        {
            var config = Substitute.For<IAzureDevOpsConfigurationStore>();
            config.GetIsEnabled().Returns(enabled);
            var adoApiClient = Substitute.For<IAdoApiClient>();
            adoApiClient.GetBuildWorkItemLinks(null).ReturnsForAnyArgs(ci => throw new InvalidOperationException());
            return new WorkItemLinkMapper(config, adoApiClient);
        }

        [Test]
        public void WhenDisabledReturnsNull()
        {
            var links = CreateWorkItemLinkMapper(false).Map("Deployable", new SemanticVersion("1.0"), new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            }, Substitute.For<ILogWithContext>());
            Assert.IsTrue(links.Succeeded);
            Assert.IsNull(links.Value);
        }
    }
}