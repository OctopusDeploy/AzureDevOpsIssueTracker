using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Octopus.Diagnostics;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    public class HttpJsonClientStatus
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public bool SignInPage { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsSuccessStatusCode()
        {
            return HttpStatusCode >= HttpStatusCode.OK
                   && HttpStatusCode <= (HttpStatusCode) 299;
        }

        public string ToDescription()
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                return ErrorMessage;
            }

            if (SignInPage)
            {
                return "The server returned a sign-in page."
                       + " Please confirm the Personal Access Token is configured correctly in Azure DevOps Issue Tracker settings.";
            }

            var description = $"{(int) HttpStatusCode} ({HttpStatusCode}).";
            if (HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                description += " Please confirm the Personal Access Token is configured correctly in Azure DevOps Issue Tracker settings.";
            }

            return description;
        }

        public static implicit operator HttpJsonClientStatus(HttpStatusCode code) => new HttpJsonClientStatus {HttpStatusCode = code};
    }

    public interface IHttpJsonClient : IDisposable
    {
        (HttpJsonClientStatus status, JObject jObject) Get(string url, string basicPassword = null);
    }

    public sealed class HttpJsonClient : IHttpJsonClient
    {
        private readonly ILogWithContext log;
        private readonly HttpClient httpClient = new HttpClient();

        public HttpJsonClient(ILogWithContext log)
        {
            this.log = log;
        }

        public (HttpJsonClientStatus status, JObject jObject) Get(string url, string basicPassword = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(basicPassword))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + basicPassword)));
            }

            log.Trace($"Azure DevOps client request: GET {url} ({(string.IsNullOrEmpty(basicPassword) ? "unauthenticated" : "authenticated")})");

            HttpResponseMessage response;
            try
            {
                response = httpClient.SendAsync(request).GetAwaiter().GetResult();
            }
            catch (HttpRequestException ex)
            {
                var message = ex.InnerException is WebException wex
                    ? wex.Message
                    : ex.Message;
                return (new HttpJsonClientStatus {ErrorMessage = message}, null);
            }

            using (response)
            {
                // Work around servers that report auth failure with redirect to a status 203 html page (in violation of our Accept header)
                if (response.Content?.Headers?.ContentType?.MediaType == "text/html"
                    && (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation
                        || response.RequestMessage.RequestUri.AbsolutePath.Contains(@"signin")))
                {
                    return (new HttpJsonClientStatus {SignInPage = true}, null);
                }

                return (
                    response.StatusCode,
                    ParseJsonOrDefault(response.Content)
                );
            }
        }

        private JObject ParseJsonOrDefault(HttpContent httpContent)
        {
            try
            {
                return JObject.Parse(httpContent.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}