using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Data;
using Octopus.Data.Model;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions.Model.BuildInformation;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Tests
{
    [TestFixture]
    public class WorkItemLinkMapperScenarios
    {
        private WorkItemLinkMapper CreateWorkItemLinkMapper(IAdoApiClient? client = null, IAzureDevOpsConfigurationStore? store = null, bool enabled = true)
        {
            var config = store ?? Substitute.For<IAzureDevOpsConfigurationStore>();
            config.GetIsEnabled().Returns(enabled);
            return new WorkItemLinkMapper(config, client ?? Substitute.For<IAdoApiClient>());
        }

        [Test]
        public void WhenDisabledReturnsExtensionIsDisabled()
        {
            var result = CreateWorkItemLinkMapper(enabled: false).Map(new OctopusBuildInformation
            {
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            });
            Assert.IsInstanceOf<IFailureResultFromDisabledExtension>(result);
        }
        
        [Test]
        public void WhenNotFromAdoReturnsEmptyList()
        {
            ISuccessResult<WorkItemLink[]> result = (ISuccessResult<WorkItemLink[]>)CreateWorkItemLinkMapper().Map(new OctopusBuildInformation
            {
                BuildEnvironment = "Something",
                BuildUrl = "http://redstoneblock/DefaultCollection/Deployable/_build/results?buildId=24"
            });
            
            Assert.IsEmpty(result.Value);
        }
        
        [Test]
        public void WhenItFindsWorkItemsWithComments()
        {
            var systemLog = Substitute.For<ISystemLog>();
            var store = Substitute.For<IAzureDevOpsConfigurationStore>();
            var jsonClient = Substitute.For<IHttpJsonClient>();
            var client = new AdoApiClient(systemLog, store, jsonClient,
                new HtmlConvert(systemLog));

            const string? baseUrl1 = "https://ado";
            const string password1 = "password1";
            const int buildId = 16;
            const int workItemOneId = 24;
            const int workItemTwoId = 25;
            const string projectName = "IssueTracker";
            
            store.GetConnections().Returns(new List<AzureDevOpsConnection>
            {
                new()
                {
                    BaseUrl = baseUrl1, 
                    PersonalAccessToken = password1.ToSensitiveString(),
                    ReleaseNoteOptions = new ReleaseNoteOptions { ReleaseNotePrefix = "My note:" }
                },
                new()
                {
                    BaseUrl = "https://anotherado", 
                    PersonalAccessToken = "password2".ToSensitiveString()
                }
            });

            jsonClient.Get($"{baseUrl1}/{projectName}/_apis/build/builds/{buildId}/workitems?api-version=4.1", password1).Returns((HttpStatusCode.OK,
                JObject.Parse($@"
{{
    ""value"": [
        {{
            ""id"": ""{workItemOneId}"",
            ""url"": ""{baseUrl1}/{projectName}/_apis/wit/workItems/{workItemOneId}""
        }},
        {{
            ""id"": ""{workItemTwoId}"",
            ""url"": ""{baseUrl1}/{projectName}/_apis/wit/workItems/{workItemTwoId}""
        }}
    ]
}}
")));

            jsonClient.Get($"{baseUrl1}/{projectName}/_apis/wit/workitems/{workItemOneId}?api-version=4.1", password1).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 3,
        ""System.Title"": ""This is the work item one title""
    }
}
")));
            jsonClient.Get($"{baseUrl1}/{projectName}/_apis/wit/workitems/{workItemOneId}/comments?api-version=4.1-preview.2", password1).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 3,
    ""fromRevisionCount"": 0,
    ""count"": 3,
    ""comments"": [
        {
            ""text"": ""<div>my first comment</div>""
        },
        {
            ""text"": ""<div>My second comment</div>""
        },
        {
            ""text"": ""<div>My note: This is a comment with the special prefix </div>""
        }
    ]
}
")));
            
            jsonClient.Get($"{baseUrl1}/{projectName}/_apis/wit/workitems/{workItemTwoId}?api-version=4.1", password1).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 1,
        ""System.Title"": ""This is the work item two title""
    }
}
")));
            jsonClient.Get($"{baseUrl1}/{projectName}/_apis/wit/workitems/{workItemTwoId}/comments?api-version=4.1-preview.2", password1).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 1,
    ""fromRevisionCount"": 0,
    ""count"": 1,
    ""comments"": [
        {
            ""text"": ""<div>Not a special comment</div>""
        }
    ]
}
")));
            
            ISuccessResult<WorkItemLink[]> result = (ISuccessResult<WorkItemLink[]>)CreateWorkItemLinkMapper(client, store).Map(new OctopusBuildInformation
            {
                BuildEnvironment = "Azure DevOps",
                BuildUrl = $"{baseUrl1}/{projectName}/_build/results?buildId={buildId}"
            });

            var workItemLinks = result.Value;
            Assert.AreEqual(2, workItemLinks.Length);
            Assert.AreEqual("This is a comment with the special prefix", workItemLinks[0].Description);
            Assert.AreEqual("This is the work item two title", workItemLinks[1].Description);
        }

        const string CommonOrg = "https://myorg";
        
        [Test]
        [TestCase(CommonOrg, "password1", "projectOne")]
        [TestCase(CommonOrg, "passwordTwo", "projectTwo")]
        [TestCase("https://adifferentorg", "anotherorg", "projectTwo")]
        public void FindsTheMostQualifiedConnection(string baseUrl, string password, string projectName)
        {
            var systemLog = Substitute.For<ISystemLog>();
            var store = Substitute.For<IAzureDevOpsConfigurationStore>();
            var jsonClient = Substitute.For<IHttpJsonClient>();
            var client = new AdoApiClient(systemLog, store, jsonClient,
                new HtmlConvert(systemLog));

            var baseUrlWithProject = $"{baseUrl}/{projectName}";
            const int buildId = 16;
            const int workItemOneId = 24;
            const int workItemTwoId = 25;
            
            store.GetConnections().Returns(new List<AzureDevOpsConnection>
            {
                new()
                {
                    BaseUrl = $"{CommonOrg}/nottobeused", 
                    PersonalAccessToken = "something secret".ToSensitiveString()
                },
                new()
                {
                    BaseUrl = baseUrlWithProject, 
                    PersonalAccessToken = password.ToSensitiveString(),
                    ReleaseNoteOptions = new ReleaseNoteOptions { ReleaseNotePrefix = "My note:" }
                },
                new()
                {
                    BaseUrl = $"{CommonOrg}/nottobeusedagain", 
                    PersonalAccessToken = "something secret".ToSensitiveString()
                },
                new()
                {
                    BaseUrl = CommonOrg, 
                    PersonalAccessToken = "password for org".ToSensitiveString()
                }
            });

            jsonClient.Get($"{baseUrlWithProject}/_apis/build/builds/{buildId}/workitems?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse($@"
{{
    ""value"": [
        {{
            ""id"": ""{workItemOneId}"",
            ""url"": ""{baseUrlWithProject}/_apis/wit/workItems/{workItemOneId}""
        }},
        {{
            ""id"": ""{workItemTwoId}"",
            ""url"": ""{baseUrlWithProject}/_apis/wit/workItems/{workItemTwoId}""
        }}
    ]
}}
")));

            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemOneId}?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 3,
        ""System.Title"": ""This is the work item one title""
    }
}
")));
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemOneId}/comments?api-version=4.1-preview.2", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 3,
    ""fromRevisionCount"": 0,
    ""count"": 3,
    ""comments"": [
        {
            ""text"": ""<div>my first comment</div>""
        },
        {
            ""text"": ""<div>My second comment</div>""
        },
        {
            ""text"": ""<div>My note: This is a comment with the special prefix </div>""
        }
    ]
}
")));
            
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemTwoId}?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 1,
        ""System.Title"": ""This is the work item two title""
    }
}
")));
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemTwoId}/comments?api-version=4.1-preview.2", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 1,
    ""fromRevisionCount"": 0,
    ""count"": 1,
    ""comments"": [
        {
            ""text"": ""<div>Not a special comment</div>""
        }
    ]
}
")));
            
            ISuccessResult<WorkItemLink[]> result = (ISuccessResult<WorkItemLink[]>)CreateWorkItemLinkMapper(client, store).Map(new OctopusBuildInformation
            {
                BuildEnvironment = "Azure DevOps",
                BuildUrl = $"{baseUrlWithProject}/_build/results?buildId={buildId}"
            });

            var workItemLinks = result.Value;
            Assert.AreEqual(2, workItemLinks.Length);
            Assert.AreEqual("This is a comment with the special prefix", workItemLinks[0].Description);
            Assert.AreEqual("This is the work item two title", workItemLinks[1].Description);
        }
        
        [Test]
        public void FallBackToOrgConnection()
        {
            var systemLog = Substitute.For<ISystemLog>();
            var store = Substitute.For<IAzureDevOpsConfigurationStore>();
            var jsonClient = Substitute.For<IHttpJsonClient>();
            var client = new AdoApiClient(systemLog, store, jsonClient,
                new HtmlConvert(systemLog));

            string baseUrl = CommonOrg;
            string password = "password for org";
            string projectName = "myproject";

            var baseUrlWithProject = $"{baseUrl}/{projectName}";
            const int buildId = 16;
            const int workItemOneId = 24;
            const int workItemTwoId = 25;

            store.GetConnections().Returns(new List<AzureDevOpsConnection>
            {
                new()
                {
                    BaseUrl = $"{CommonOrg}/nottobeused", 
                    PersonalAccessToken = "something secret".ToSensitiveString()
                },
                new()
                {
                    BaseUrl = $"{CommonOrg}/nottobeusedagain", 
                    PersonalAccessToken = "something secret".ToSensitiveString()
                },
                new()
                {
                    BaseUrl = CommonOrg, 
                    PersonalAccessToken = password.ToSensitiveString(),
                    ReleaseNoteOptions = new ReleaseNoteOptions { ReleaseNotePrefix = "My note:" }
                }
            });

            jsonClient.Get($"{baseUrlWithProject}/_apis/build/builds/{buildId}/workitems?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse($@"
{{
    ""value"": [
        {{
            ""id"": ""{workItemOneId}"",
            ""url"": ""{baseUrlWithProject}/_apis/wit/workItems/{workItemOneId}""
        }},
        {{
            ""id"": ""{workItemTwoId}"",
            ""url"": ""{baseUrlWithProject}/_apis/wit/workItems/{workItemTwoId}""
        }}
    ]
}}
")));

            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemOneId}?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 3,
        ""System.Title"": ""This is the work item one title""
    }
}
")));
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemOneId}/comments?api-version=4.1-preview.2", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 3,
    ""fromRevisionCount"": 0,
    ""count"": 3,
    ""comments"": [
        {
            ""text"": ""<div>my first comment</div>""
        },
        {
            ""text"": ""<div>My second comment</div>""
        },
        {
            ""text"": ""<div>My note: This is a comment with the special prefix </div>""
        }
    ]
}
")));
            
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemTwoId}?api-version=4.1", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""fields"": {
        ""System.CommentCount"": 1,
        ""System.Title"": ""This is the work item two title""
    }
}
")));
            jsonClient.Get($"{baseUrlWithProject}/_apis/wit/workitems/{workItemTwoId}/comments?api-version=4.1-preview.2", password).Returns((HttpStatusCode.OK,
                JObject.Parse(@"
{
    ""totalCount"": 1,
    ""fromRevisionCount"": 0,
    ""count"": 1,
    ""comments"": [
        {
            ""text"": ""<div>Not a special comment</div>""
        }
    ]
}
")));
            
            ISuccessResult<WorkItemLink[]> result = (ISuccessResult<WorkItemLink[]>)CreateWorkItemLinkMapper(client, store).Map(new OctopusBuildInformation
            {
                BuildEnvironment = "Azure DevOps",
                BuildUrl = $"{baseUrlWithProject}/_build/results?buildId={buildId}"
            });

            var workItemLinks = result.Value;
            Assert.AreEqual(2, workItemLinks.Length);
            Assert.AreEqual("This is a comment with the special prefix", workItemLinks[0].Description);
            Assert.AreEqual("This is the work item two title", workItemLinks[1].Description);
        }
    }
}