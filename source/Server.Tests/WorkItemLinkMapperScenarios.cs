using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;

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
            adoApiClient.GetBuildWorkItemLinks(new AdoBuildUrls("http://redstoneblock", 24)).ReturnsForAnyArgs(ci => throw new InvalidOperationException());
            return new WorkItemLinkMapper(config, adoApiClient);
        }

        [Test]
        public void WhenDisabledReturnsExtensionIsDisabled()
        {
            var links = CreateWorkItemLinkMapper(false).Map(new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            });
            Assert.IsInstanceOf<IFailureResultFromDisabledExtension>(links);
        }
    }
}