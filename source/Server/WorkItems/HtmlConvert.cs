using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using Octopus.Diagnostics;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems
{
    internal class HtmlConvert
    {
        private static readonly IReadOnlyDictionary<string, bool> NewlineElementNames =
            // ReSharper disable once StringLiteralTypo
            "br address article aside blockquote dd details dialog div dl dt fieldset figcaption figure footer form h1 h2 h3 h4 h5 h6 header hgroup hr li main nav ol p pre section table ul"
                .Split(' ')
                .ToDictionary(n => n, n => true);

        private readonly ISystemLog systemLog;

        public HtmlConvert(ISystemLog systemLog)
        {
            this.systemLog = systemLog;
        }

        public string ToPlainText(string html)
        {
            try
            {
                var sb = new StringBuilder();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // Custom traversal because HtmlAgilityPack's InnerText doesn't handle entities correctly or convert whitespace
                AppendPlainText(sb, doc.DocumentNode);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                systemLog.Info(ex, "Unable to convert Azure DevOps work item comment HTML to plain text.");
                return html;
            }
        }

        internal static void AppendPlainText(StringBuilder sb, HtmlNode node)
        {
            if (node is HtmlTextNode)
            {
                sb.Append(HtmlEntity.DeEntitize(node.InnerHtml));
                return;
            }

            AppendAnyNewlines(sb, node);

            if (node.ChildNodes.Count > 0)
            {
                foreach (var child in node.ChildNodes) AppendPlainText(sb, child);

                AppendAnyNewlines(sb, node);
            }
        }

        internal static void AppendAnyNewlines(StringBuilder sb, HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Element && NewlineElementNames.ContainsKey(node.Name))
            {
                sb.AppendLine();
                if ("p".Equals(node.Name, StringComparison.OrdinalIgnoreCase)) sb.AppendLine();
            }
        }
    }
}