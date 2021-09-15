using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Data;
using Octopus.Data.Model;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.HostServices.Model;
using Octopus.Server.Extensibility.HostServices.Model.Spaces;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Mediator;
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Features.IssueTrackers;
using Octopus.Server.MessageContracts.Features.Spaces;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class AdoApiClientScenarios
    {
        private static readonly HtmlConvert HtmlConvert = new HtmlConvert(Substitute.For<ISystemLog>());
        private ISystemLog? log;

        private static IAzureDevOpsConfigurationStore CreateSubstituteStore()
        {
            var store = Substitute.For<IAzureDevOpsConfigurationStore>();
            store.GetBaseUrl().Returns("http://redstoneblock/DefaultCollection/");
            store.GetPersonalAccessToken().Returns("rumor".ToSensitiveString());
            store.GetReleaseNotePrefix().Returns("= Changelog =");
            return store;
        }

        private static GetExtensionSettingsResponse<AzureDevOpsConfigurationOverride> CreateGetExtensionSettingsResponse(bool isOverriding)
        {
            var spaceSettings = new AzureDevOpsConfigurationOverride()
            {
                IsOverriding = isOverriding,
                Settings = new Setting[]
                {
                    new Setting
                    {
                        PersonalAccessToken =  "spaceRumor".ToSensitiveString(),
                        BaseUrl = "http://redstoneblock/DefaultCollection/"
                    }
                }
            };
            return new GetExtensionSettingsResponse<AzureDevOpsConfigurationOverride>(spaceSettings);
        }

        private static IMediator CreateSubstituteMediator(bool isOverriding = false)
        {
            var mediator = Substitute.For<IMediator>();
            mediator.Request(
                Arg.Any<GetSpaceExtensionSettingsRequest<AzureDevOpsConfigurationOverride>>(), 
                Arg.Any<CancellationToken>())
                .Returns(CreateGetExtensionSettingsResponse(isOverriding));

            return mediator;
        }

        [SetUp]
        public void SetUp()
        {
            log = Substitute.For<ISystemLog>();
        }

        [Test]
        public async Task ClientCanRequestAndParseWorkItemsRefsAndLinks()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/24/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""2"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/2""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/2?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":2,""fields"":{""System.CommentCount"":0,""System.Title"": ""README has no useful content""}}")));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"), "Space-1".ToSpaceId(), CancellationToken.None);

            var workItemLink = ((ISuccessResult<WorkItemLink[]>)workItemLinks).Value.Single();
            Assert.AreEqual("2", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=2", workItemLink.LinkUrl);
            Assert.AreEqual("README has no useful content", workItemLink.Description);
        }


        [Test]
        public async Task SourceGetsSet()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/24/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""2"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/2""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/2?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":2,""fields"":{""System.CommentCount"":0,""System.Title"": ""README has no useful content""}}")));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"), "Space-1".ToSpaceId(), CancellationToken.None);

            var workItemLink = ((ISuccessResult<WorkItemLink[]>)workItemLinks).Value.Single();
            Assert.AreEqual("Azure DevOps", workItemLink.Source);
        }

        [Test]
        public async Task ClientCanRequestAndParseWorkItemsWithReleaseNotes()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/28/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""4"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/4""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/4?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":4,""fields"":{""System.CommentCount"":3,""System.Title"":""The README riddle has no answer""}}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/4/comments?api-version=4.1-preview.2", "rumor")
                .Returns((HttpStatusCode.OK, JObject.Parse(@"{""totalCount"":3,""count"":3,""comments"":[{""text"":""= Changelog = N/A""}," +
                                                           @"{""text"":""<div>= Changelog =&nbsp;README <i>riddle</i> now has an answer!</div>""},{""text"":""See also related issue.""}]}")));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=28"), "Space-1".ToSpaceId(), CancellationToken.None);

            var workItemLink = ((ISuccessResult<WorkItemLink[]>)workItemLinks).Value.Single();
            Assert.AreEqual("4", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=4", workItemLink.LinkUrl);
            Assert.AreEqual("README riddle now has an answer!", workItemLink.Description);
        }

        [Test]
        public async Task ClientReportsFailuresAndReturnsPartialResults()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/29/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":2,""value"":[{""id"":""5"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/5""},"
                                  + @"{""id"":""6"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/6""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/5?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":5,""fields"":{""System.CommentCount"":3,""System.Title"":""The README riddle has no answer""}}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/5/comments?api-version=4.1-preview.2", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/6?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/6/comments?api-version=4.1-preview.2", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=29"), "Space-1".ToSpaceId(), CancellationToken.None);

            var successResult = ((ISuccessResult<WorkItemLink[]>)workItemLinks);
            Assert.AreEqual(2, successResult.Value.Length);
            Assert.AreEqual("5", successResult.Value[0].Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=5", successResult.Value[0].LinkUrl);
            Assert.AreEqual("The README riddle has no answer", successResult.Value[0].Description);
            Assert.AreEqual("6", successResult.Value[1].Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=6", successResult.Value[1].LinkUrl);
            Assert.AreEqual("6", successResult.Value[1].Description);
            log!.Received().WarnFormat(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }

        [Test]
        public async Task PersonalAccessTokenIsOnlySentToItsOrigin()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            string passwordSent = ".";
            httpJsonClient.Get(Arg.Any<string>())
                .ReturnsForAnyArgs(ci =>
                {
                    passwordSent = ci.ArgAt<string>(1);
                    return (HttpStatusCode.OK, JObject.Parse(@"{""count"":0,""value"":[]}"));
                });

            // Request to origin should include password
            await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator)
                .GetBuildWorkItemsRefs(AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"), "rumor".ToSensitiveString(), CancellationToken.None);
            Assert.AreEqual("rumor", passwordSent);
        }

        [Test]
        public async Task AcceptsDeletedBuildAsPermanentEmptySet()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/7/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.NotFound,
                    JObject.Parse(@"{""$id"":""1"",""message"":""The requested build 7 could not be found."",""errorCode"":0,""eventId"":3000}")));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=7"), "Space-1".ToSpaceId(), CancellationToken.None);

            Assert.IsEmpty(((ISuccessResult<WorkItemLink[]>)workItemLinks).Value);
        }

        [Test]
        public async Task AcceptsDeletedWorkItemAsPermanentMissingTitle()
        {
            var store = CreateSubstituteStore();
            var mediator = CreateSubstituteMediator();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/8/workitems?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""999"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/999""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/999?api-version=4.1", "rumor")
                .Returns((HttpStatusCode.NotFound,
                    JObject.Parse(@"{""$id"":""1"",""message"":""TF401232: Work item 999 does not exist."",""errorCode"":0,""eventId"":3200}")));

            var workItemLinks = await new AdoApiClient(log!, store, httpJsonClient, HtmlConvert, mediator).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=8"), "Space-1".ToSpaceId(), CancellationToken.None);

            var workItemLink = ((ISuccessResult<WorkItemLink[]>)workItemLinks).Value.Single();
            Assert.AreEqual("999", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=999", workItemLink.LinkUrl);
            Assert.AreEqual("999", workItemLink.Description);
        }
    }
}