using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class AdoApiClientScenarios
    {
        private static readonly ILogWithContext Log = Substitute.For<ILogWithContext>();
        private static readonly HtmlConvert HtmlConvert = new HtmlConvert(Log);

        private static IAzureDevOpsConfigurationStore CreateSubstituteStore()
        {
            var store = Substitute.For<IAzureDevOpsConfigurationStore>();
            store.GetBaseUrl().Returns("http://redstoneblock/DefaultCollection/");
            store.GetPersonalAccessToken().Returns("rumor");
            store.GetReleaseNotePrefix().Returns("= Changelog =");
            return store;
        }

        [Test]
        public void ClientCanRequestAndParseWorkItemsRefsAndLinks()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/24/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""2"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/2""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/2?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":2,""fields"":{""System.CommentCount"":0,""System.Title"": ""README has no useful content""}}")));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"));

            Assert.IsTrue(workItemLinks.Succeeded);
            var workItemLink = workItemLinks.Value.Single();
            Assert.AreEqual("2", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=2", workItemLink.LinkUrl);
            Assert.AreEqual("README has no useful content", workItemLink.Description);
        }

        [Test]
        public void SourceGetsSet()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/24/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""2"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/2""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/2?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":2,""fields"":{""System.CommentCount"":0,""System.Title"": ""README has no useful content""}}")));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"));

            Assert.IsTrue(workItemLinks.Succeeded);
            var workItemLink = workItemLinks.Value.Single();
            Assert.AreEqual("Azure DevOps", workItemLink.Source);
        }

        [Test]
        public void ClientCanRequestAndParseWorkItemsWithReleaseNotes()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/28/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""4"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/4""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/4?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":4,""fields"":{""System.CommentCount"":3,""System.Title"":""The README riddle has no answer""}}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/4/comments?api-version=5.0-preview.2", "rumor")
                .Returns((HttpStatusCode.OK, JObject.Parse(@"{""totalCount"":3,""count"":3,""comments"":[{""text"":""= Changelog = N/A""}," +
                                                           @"{""text"":""<div>= Changelog =&nbsp;README <i>riddle</i> now has an answer!</div>""},{""text"":""See also related issue.""}]}")));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=28"));

            Assert.IsTrue(workItemLinks.Succeeded);
            var workItemLink = workItemLinks.Value.Single();
            Assert.AreEqual("4", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=4", workItemLink.LinkUrl);
            Assert.AreEqual("README riddle now has an answer!", workItemLink.Description);
        }

        [Test]
        public void ClientReportsFailuresAndReturnsPartialResults()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/29/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":2,""value"":[{""id"":""5"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/5""},"
                                  + @"{""id"":""6"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/6""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/5?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""id"":5,""fields"":{""System.CommentCount"":3,""System.Title"":""The README riddle has no answer""}}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/5/comments?api-version=5.0-preview.2", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/6?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/6/comments?api-version=5.0-preview.2", "rumor")
                .Returns((HttpStatusCode.InternalServerError, null));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=29"));

            Assert.IsFalse(workItemLinks.Succeeded);
            Assert.AreEqual("Error while fetching work item comments from Azure DevOps: 500 (InternalServerError).", workItemLinks.FailureReason);
            Assert.AreEqual(2, workItemLinks.Value.Length);
            Assert.AreEqual("5", workItemLinks.Value[0].Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=5", workItemLinks.Value[0].LinkUrl);
            Assert.AreEqual("The README riddle has no answer", workItemLinks.Value[0].Description);
            Assert.AreEqual("6", workItemLinks.Value[1].Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=6", workItemLinks.Value[1].LinkUrl);
            Assert.AreEqual("6", workItemLinks.Value[1].Description);
        }

        [Test]
        public void PersonalAccessTokenIsOnlySentToItsOrigin()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            string passwordSent = ".";
            httpJsonClient.Get(null)
                .ReturnsForAnyArgs(ci =>
                {
                    passwordSent = ci.ArgAt<string>(1);
                    return (HttpStatusCode.OK, JObject.Parse(@"{""count"":0,""value"":[]}"));
                });

            // Request to other host should not include password
            new AdoApiClient(store, httpJsonClient, HtmlConvert, Log)
                .GetBuildWorkItemsRefs(AdoBuildUrls.ParseBrowserUrl("http://someotherhost/DefaultCollection/Deployable/_build/results?buildId=24"));
            Assert.IsNull(passwordSent);

            // Request to origin should include password
            new AdoApiClient(store, httpJsonClient, HtmlConvert, Log)
                .GetBuildWorkItemsRefs(AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"));
            Assert.AreEqual("rumor", passwordSent);
        }

        [Test]
        public void AcceptsDeletedBuildAsPermanentEmptySet()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/7/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.NotFound,
                    JObject.Parse(@"{""$id"":""1"",""message"":""The requested build 7 could not be found."",""errorCode"":0,""eventId"":3000}")));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=7"));

            Assert.IsTrue(workItemLinks.Succeeded);
            Assert.IsEmpty(workItemLinks.Value);
        }

        [Test]
        public void AcceptsDeletedWorkItemAsPermanentMissingTitle()
        {
            var store = CreateSubstituteStore();
            var httpJsonClient = Substitute.For<IHttpJsonClient>();
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/build/builds/8/workitems?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.OK,
                    JObject.Parse(@"{""count"":1,""value"":[{""id"":""999"",""url"":""http://redstoneblock/DefaultCollection/_apis/wit/workItems/999""}]}")));
            httpJsonClient.Get("http://redstoneblock/DefaultCollection/Deployable/_apis/wit/workitems/999?api-version=5.0", "rumor")
                .Returns((HttpStatusCode.NotFound,
                    JObject.Parse(@"{""$id"":""1"",""message"":""TF401232: Work item 999 does not exist."",""errorCode"":0,""eventId"":3200}")));

            var workItemLinks = new AdoApiClient(store, httpJsonClient, HtmlConvert, Log).GetBuildWorkItemLinks(
                AdoBuildUrls.ParseBrowserUrl("http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=8"));

            Assert.IsTrue(workItemLinks.Succeeded);
            var workItemLink = workItemLinks.Value.Single();
            Assert.AreEqual("999", workItemLink.Id);
            Assert.AreEqual("http://redstoneblock/DefaultCollection/Deployable/_workitems?_a=edit&id=999", workItemLink.LinkUrl);
            Assert.AreEqual("999", workItemLink.Description);
        }
    }
}