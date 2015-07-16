// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;
using Microsoft.Framework.WebEncoders;

namespace Microsoft.AspNet.Mvc.Razor.TagHelpers
{
    /// <summary>
    /// <see cref="ITagHelper"/> implementation targeting elements containing attributes with url expected values.
    /// </summary>
    /// <remarks>Resolves application relative URLs.</remarks>
    [TargetElement("*", Attributes = "itemid")]
    [TargetElement("a", Attributes = "href")]
    [TargetElement("area", Attributes = "href")]
    [TargetElement("link", Attributes = "href")]
    [TargetElement("base", Attributes = "href")]
    [TargetElement("video", Attributes = "poster")]
    [TargetElement("video", Attributes = "src")]
    [TargetElement("audio", Attributes = "src")]
    [TargetElement("embed", Attributes = "src")]
    [TargetElement("iframe", Attributes = "src")]
    [TargetElement("img", Attributes = "src")]
    [TargetElement("script", Attributes = "src")]
    [TargetElement("source", Attributes = "src")]
    [TargetElement("track", Attributes = "src")]
    [TargetElement("input", Attributes = "src")]
    [TargetElement("input", Attributes = "formaction")]
    [TargetElement("button", Attributes = "formaction")]
    [TargetElement("form", Attributes = "action")]
    [TargetElement("blockquote", Attributes = "cite")]
    [TargetElement("del", Attributes = "cite")]
    [TargetElement("ins", Attributes = "cite")]
    [TargetElement("q", Attributes = "cite")]
    [TargetElement("menuitem", Attributes = "icon")]
    [TargetElement("html", Attributes = "manifest")]
    [TargetElement("object", Attributes = "data")]
    [TargetElement("object", Attributes = "archive")]
    [TargetElement("applet", Attributes = "archive")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class UrlResolutionTagHelper : TagHelper
    {
        private static readonly IReadOnlyDictionary<string, IEnumerable<string>> ElementAttributeLookups =
            new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", new[] { "href" } },
                { "area", new[] { "href" } },
                { "link", new[] { "href" } },
                { "base", new[] { "href" } },
                { "video", new[] { "poster", "src" } },
                { "audio", new[] { "src" } },
                { "embed", new[] { "src" } },
                { "iframe", new[] { "src" } },
                { "img", new[] { "src" } },
                { "script", new[] { "src" } },
                { "source", new[] { "src" } },
                { "track", new[] { "src" } },
                { "input", new[] { "src", "formaction" } },
                { "button", new[] { "formaction" } },
                { "form", new[] { "action" } },
                { "blockquote", new[] { "cite" } },
                { "del", new[] { "cite" } },
                { "ins", new[] { "cite" } },
                { "q", new[] { "cite" } },
                { "menuitem", new[] { "icon" } },
                { "html", new[] { "manifest" } },
                { "object", new[] { "data", "archive" } },
                { "applet", new[] { "archive" } },
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
                return int.MinValue;
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
            while (++prefixEndIndex < urlLength && char.IsWhiteSpace(url[prefixEndIndex])) ;

            // Before doing more work, ensure that the URL we're looking at is app relative.
            if (prefixEndIndex < urlLength - 2 && url[prefixEndIndex] == '~' && url[prefixEndIndex + 1] == '/')
            {
                var valueEndIndex = prefixEndIndex - 1;
                while (++valueEndIndex < urlLength && !char.IsWhiteSpace(url[valueEndIndex])) ;

                var prefix = url.Substring(0, prefixEndIndex);
                var urlValue = url.Substring(prefixEndIndex, valueEndIndex - prefixEndIndex);
                var appRelativeUrl = UrlHelper.Content(urlValue);
                var suffix = url.Substring(valueEndIndex);

                if (tryEncodeApplicationPath)
                {
                    var postTildaSlashUrlValue = urlValue.Substring(2);

                    if (!appRelativeUrl.EndsWith(postTildaSlashUrlValue, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            Resources.FormatCouldNotResolveApplicationRelativeUrl_TagHelper(
                                url,
                                nameof(IUrlHelper),
                                nameof(IUrlHelper.Content),
                                typeof(UrlResolutionTagHelper).FullName,
                                typeof(UrlResolutionTagHelper).GetTypeInfo().Assembly.GetName().Name));
                    }

                    var applicationPath = appRelativeUrl.Substring(0, appRelativeUrl.Length - postTildaSlashUrlValue.Length);
                    var encodedApplicationPath = HtmlEncoder.HtmlEncode(applicationPath);

                    resolvedUrl = string.Join(string.Empty, prefix, encodedApplicationPath, postTildaSlashUrlValue, suffix);
                }
                else
                {
                    resolvedUrl = string.Join(string.Empty, prefix, appRelativeUrl, suffix);
                }

                return true;
            }

            return false;
        }
    }
}
