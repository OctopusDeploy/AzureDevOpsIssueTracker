using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    class HttpJsonClientStatus
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public bool SignInPage { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool IsSuccessStatusCode()
        {
            return HttpStatusCode >= HttpStatusCode.OK
                   && HttpStatusCode <= (HttpStatusCode) 299;
        }

        public string ToDescription(JObject? jObject = null, bool disableSettingsHints = false)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                return ErrorMessage;
            }

            var authMessage = disableSettingsHints
                ? $" Please confirm you are using a Personal Access Token authorized {HttpJsonClient.AuthMessageScope}."
                : $" Please confirm the Personal Access Token is configured correctly in Azure DevOps Issue Tracker settings, and is authorized {HttpJsonClient.AuthMessageScope}.";
            if (SignInPage)
            {
                return "The server returned a sign-in page." + authMessage;
            }

            var description = $"{(int) HttpStatusCode} ({HttpStatusCode}).";
            var bodyMessage = jObject?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(bodyMessage))
            {
                description += $" \"{bodyMessage}\"";
            }

            if (HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                description += authMessage;
            }

            return description;
        }

        public static implicit operator HttpJsonClientStatus(HttpStatusCode code) => new HttpJsonClientStatus {HttpStatusCode = code};
    }

    interface IHttpJsonClient : IDisposable
    {
        (HttpJsonClientStatus status, JObject? jObject) Get(string url, string? basicPassword = null);
    }

    sealed class HttpJsonClient : IHttpJsonClient
    {
        public static string AuthMessageScope = "for this scope";

        private readonly HttpClient httpClient;

        public HttpJsonClient(IOctopusHttpClientFactory octopusHttpClientFactory)
        {
            httpClient = octopusHttpClientFactory.CreateClient();
        }
        
        public (HttpJsonClientStatus status, JObject? jObject) Get(string url, string? basicPassword = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(basicPassword))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + basicPassword)));
            }

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

        private JObject? ParseJsonOrDefault(HttpContent? httpContent)
        {
            if (httpContent == null)
                return null;
            
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