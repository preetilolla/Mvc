// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;
using Microsoft.Framework.WebEncoders;

namespace Microsoft.AspNet.Mvc.Razor.TagHelpers
{
    /// <summary>
    /// <see cref="ITagHelper"/> implementation targeting elements containing attributes with URL expected values.
    /// </summary>
    /// <remarks>Resolves application relative URLs that are not targeted by other <see cref="ITagHelper"/>s. Runs
    /// prior to other <see cref="ITagHelper"/>s to ensure application-relative URLs are resolved.</remarks>
    [TargetElement("*", Attributes = "itemid")]
    [TargetElement("a", Attributes = "href")]
    [TargetElement("applet", Attributes = "archive")]
    [TargetElement("area", Attributes = "href")]
    [TargetElement("audio", Attributes = "src")]
    [TargetElement("base", Attributes = "href")]
    [TargetElement("blockquote", Attributes = "cite")]
    [TargetElement("button", Attributes = "formaction")]
    [TargetElement("del", Attributes = "cite")]
    [TargetElement("embed", Attributes = "src")]
    [TargetElement("form", Attributes = "action")]
    [TargetElement("html", Attributes = "manifest")]
    [TargetElement("iframe", Attributes = "src")]
    [TargetElement("img", Attributes = "src")]
    [TargetElement("input", Attributes = "src")]
    [TargetElement("input", Attributes = "formaction")]
    [TargetElement("ins", Attributes = "cite")]
    [TargetElement("link", Attributes = "href")]
    [TargetElement("menuitem", Attributes = "icon")]
    [TargetElement("object", Attributes = "archive")]
    [TargetElement("object", Attributes = "data")]
    [TargetElement("q", Attributes = "cite")]
    [TargetElement("script", Attributes = "src")]
    [TargetElement("source", Attributes = "src")]
    [TargetElement("track", Attributes = "src")]
    [TargetElement("video", Attributes = "src")]
    [TargetElement("video", Attributes = "poster")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class UrlResolutionTagHelper : TagHelper
    {
        // Valid whitespace characters defined by the HTML5 spec.
        private static readonly char[] ValidAttributeWhitespaceChars =
            new[] { '\u0009', '\u000A', '\u000C', '\u000D' };
        private static readonly IReadOnlyDictionary<string, IEnumerable<string>> ElementAttributeLookups =
            new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", new[] { "href" } },
                { "applet", new[] { "archive" } },
                { "area", new[] { "href" } },
                { "audio", new[] { "src" } },
                { "base", new[] { "href" } },
                { "blockquote", new[] { "cite" } },
                { "button", new[] { "formaction" } },
                { "del", new[] { "cite" } },
                { "embed", new[] { "src" } },
                { "form", new[] { "action" } },
                { "html", new[] { "manifest" } },
                { "iframe", new[] { "src" } },
                { "img", new[] { "src" } },
                { "input", new[] { "src", "formaction" } },
                { "ins", new[] { "cite" } },
                { "link", new[] { "href" } },
                { "menuitem", new[] { "icon" } },
                { "object", new[] { "archive", "data" } },
                { "q", new[] { "cite" } },
                { "script", new[] { "src" } },
                { "source", new[] { "src" } },
                { "track", new[] { "src" } },
                { "video", new[] { "poster", "src" } },
            };

        /// <summary>
        /// Creates a new <see cref="UrlResolutionTagHelper"/>.
        /// </summary>
        /// <param name="urlHelper">The <see cref="IUrlHelper"/>.</param>
        /// <param name="htmlEncoder">The <see cref="IHtmlEncoder"/>.</param>
        public UrlResolutionTagHelper(IUrlHelper urlHelper, IHtmlEncoder htmlEncoder)
        {
            UrlHelper = urlHelper;
            HtmlEncoder = htmlEncoder;
        }

        /// <inheritdoc />
        public override int Order
        {
            get
            {
                return DefaultOrder.DefaultFrameworkSortOrder - 999;
            }
        }

        protected IUrlHelper UrlHelper { get; }

        protected IHtmlEncoder HtmlEncoder { get; }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            IEnumerable<string> attributeNames;
            if (ElementAttributeLookups.TryGetValue(output.TagName, out attributeNames))
            {
                foreach (var attributeName in attributeNames)
                {
                    ProcessUrlAttribute(attributeName, output);
                }
            }

            // itemid can be present on any HTML element.
            ProcessUrlAttribute("itemid", output);
        }

        private void ProcessUrlAttribute(string attributeName, TagHelperOutput output)
        {
            IEnumerable<TagHelperAttribute> attributes;
            if (output.Attributes.TryGetAttributes(attributeName, out attributes))
            {
                foreach (var attribute in attributes)
                {
                    string resolvedUrl;
                    if (attribute.Value is string)
                    {
                        if (TryResolveUrl(
                                (string)attribute.Value,
                                tryEncodeApplicationPath: false,
                                resolvedUrl: out resolvedUrl))
                        {
                            attribute.Value = resolvedUrl;
                        }
                    }
                    else if (attribute.Value is HtmlString)
                    {
                        var htmlStringValue = ((HtmlString)attribute.Value).ToString();
                        if (TryResolveUrl(
                            htmlStringValue,
                            tryEncodeApplicationPath: true,
                            resolvedUrl: out resolvedUrl))
                        {
                            attribute.Value = new HtmlString(resolvedUrl);
                        }
                    }
                }
            }
        }

        private bool TryResolveUrl(string url, bool tryEncodeApplicationPath, out string resolvedUrl)
        {
            resolvedUrl = null;

            if (url == null)
            {
                return false;
            }

            var urlLength = url.Length;

            // Find the start of the potential application relative URL value.
            var prefixEndIndex = -1;
            while (++prefixEndIndex < urlLength && ValidAttributeWhitespaceChars.Contains(url[prefixEndIndex])) { }

            // Before doing more work, ensure that the URL we're looking at is app relative.
            if (prefixEndIndex < urlLength - 2 && url[prefixEndIndex] == '~' && url[prefixEndIndex + 1] == '/')
            {
                var valueEndIndex = prefixEndIndex - 1;
                while (++valueEndIndex < urlLength && !ValidAttributeWhitespaceChars.Contains(url[valueEndIndex])) { }

                var prefix = url.Substring(0, prefixEndIndex);
                var urlValue = url.Substring(prefixEndIndex, valueEndIndex - prefixEndIndex);
                var appRelativeUrl = UrlHelper.Content(urlValue);
                var suffix = url.Substring(valueEndIndex);

                if (tryEncodeApplicationPath)
                {
                    var postTildeSlashUrlValue = urlValue.Substring(2);

                    if (!appRelativeUrl.EndsWith(postTildeSlashUrlValue, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            Resources.FormatCouldNotResolveApplicationRelativeUrl_TagHelper(
                                url,
                                nameof(IUrlHelper),
                                nameof(IUrlHelper.Content),
                                "removeTagHelper",
                                typeof(UrlResolutionTagHelper).FullName,
                                typeof(UrlResolutionTagHelper).GetTypeInfo().Assembly.GetName().Name));
                    }

                    var applicationPath = appRelativeUrl.Substring(0, appRelativeUrl.Length - postTildeSlashUrlValue.Length);
                    var encodedApplicationPath = HtmlEncoder.HtmlEncode(applicationPath);

                    resolvedUrl = string.Concat(prefix, encodedApplicationPath, postTildeSlashUrlValue, suffix);
                }
                else
                {
                    resolvedUrl = string.Concat(prefix, appRelativeUrl, suffix);
                }

                return true;
            }

            return false;
        }
    }
}
