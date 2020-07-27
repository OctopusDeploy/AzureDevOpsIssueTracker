using System;
using System.Web;
using System.Text.RegularExpressions;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    class AdoUrl
    {
        public AdoUrl(string organizationUrl)
        {
            OrganizationUrl = organizationUrl;
        }

        public string OrganizationUrl { get; }
    }

    class AdoProjectUrls : AdoUrl
    {
        public AdoProjectUrls(string organizationUrl) : base(organizationUrl)
        {
        }

        public string? ProjectUrl { get; set; }

        public static AdoProjectUrls ParseOrganizationAndProjectUrls(string organizationOrProjectUrl)
        {
            var uri = new Uri(organizationOrProjectUrl);

            {
                if (string.Equals(uri.Host, "dev.azure.com", StringComparison.OrdinalIgnoreCase)
                    && Regex.Match(uri.AbsolutePath, @"^/(?<org>[^/]+)(/(?<proj>[^_./:\\~&%;@'""?<>|#$*][^/:\\~&%;@'""?<>|#$*]*))?")
                        is Match match && match.Success)
                {
                    var orgUri = new Uri(uri, $"/{match.Groups["org"].Value}");
                    return new AdoProjectUrls(orgUri.AbsoluteUri)
                    {
                        ProjectUrl = match.Groups["proj"].Success
                            ? $"{orgUri.AbsoluteUri}/{match.Groups["proj"].Value}"
                            : null
                    };
                }
            }

            {
                var match = Regex.Match(uri.AbsolutePath,
                    @"^(?<prefix>/[^_./:\\~&%;@'""?<>|#$*][^/:\\~&%;@'""?<>|#$*]*)?/[^_./:\\~&%;@'""?<>|#$*][^/:\\~&%;@'""?<>|#$*]*");
                var isVsUrl = Regex.IsMatch(uri.Host, @"^[^.]+\.visualstudio\.com$");
                var projectIsSpecified = match.Success
                                         && (match.Groups["prefix"].Success || isVsUrl);
                var collectionPath = match.Groups["prefix"].Success
                    ? match.Groups["prefix"].Value
                    : isVsUrl /* For VS URLs a single identifier is likely to be a project */
                        ? "/"
                        : match.Value;

                var organizationUrl = new Uri(uri, collectionPath).AbsoluteUri;

                return new AdoProjectUrls(organizationUrl)
                {
                    ProjectUrl = projectIsSpecified
                        ? new Uri(uri, match.Value).AbsoluteUri
                        : null
                };
            }
        }
    }

    class AdoBuildUrls : AdoProjectUrls
    {
        public AdoBuildUrls(string organizationUrl, int buildId) : base(organizationUrl)
        {
            BuildId = buildId;
        }

        public int BuildId { get; set; }

        public static AdoBuildUrls ParseBrowserUrl(string browserUrl)
        {
            ArgumentException ParseError(Exception? innerException = null)
                => new ArgumentException("Unrecognized build browse URL.", nameof(browserUrl), innerException);

            try
            {
                var prefixMatch = Regex.Match(browserUrl, @"^\s*((https?://.+?)/+[^\/]+)/+_build\b");
                if (!prefixMatch.Success)
                {
                    throw ParseError();
                }

                var fullUri = new Uri(browserUrl);
                var queryParams = HttpUtility.ParseQueryString(fullUri.Query);
                var buildId = queryParams["buildId"];
                return new AdoBuildUrls(prefixMatch.Groups[2].Value, int.Parse(buildId))
                {
                    ProjectUrl = prefixMatch.Groups[1].Value
                };
            }
            catch (Exception ex)
            {
                throw ParseError(ex);
            }
        }

        public static AdoBuildUrls Create(AdoProjectUrls adoProjectUrls, int buildId)
        {
            return new AdoBuildUrls(adoProjectUrls.OrganizationUrl, buildId) { ProjectUrl = adoProjectUrls.ProjectUrl };
        }
    }
}