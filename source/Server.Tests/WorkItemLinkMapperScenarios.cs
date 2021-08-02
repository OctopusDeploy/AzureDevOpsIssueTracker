using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;
using Octopus.Server.MessageContracts.Features.BuildInformation;
using Octopus.Server.MessageContracts.Features.IssueTrackers;
using Octopus.Server.MessageContracts.Features.Spaces;

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
            adoApiClient.GetBuildWorkItemLinks(Arg.Any<AdoBuildUrls>(), Arg.Any<SpaceId>(), Arg.Any<CancellationToken>()).Returns(ci =>  Task.FromException(new InvalidOperationException()));
            return new WorkItemLinkMapper(config, adoApiClient);
        }

        [Test]
        public async Task WhenDisabledReturnsExtensionIsDisabled()
        {
            var links = await CreateWorkItemLinkMapper(false).Map("Spaces-1".ToSpaceId(), new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            }, CancellationToken.None);
            Assert.IsInstanceOf<IFailureResultFromDisabledExtension>(links);
        }
    }
}