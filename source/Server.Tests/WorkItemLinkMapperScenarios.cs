using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;
using Octopus.Server.MessageContracts.Features.BuildInformation;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class WorkItemLinkMapperScenarios
    {
        private WorkItemLinkMapper CreateWorkItemLinkMapper(bool enabled)
        {
            var config = Substitute.For<IAzureDevOpsConfigurationStore>();
            config.GetIsEnabled(CancellationToken.None).Returns(enabled);
            var adoApiClient = Substitute.For<IAdoApiClient>();
            adoApiClient.GetBuildWorkItemLinks(new AdoBuildUrls("http://redstoneblock", 24), CancellationToken.None).Throws<InvalidOperationException>();
            return new WorkItemLinkMapper(config, adoApiClient);
        }

        [Test]
        public async Task WhenDisabledReturnsExtensionIsDisabled()
        {
            var links = await CreateWorkItemLinkMapper(false).Map(new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            }, CancellationToken.None);
            Assert.IsInstanceOf<IFailureResultFromDisabledExtension>(links);
        }
    }
}