// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.FileProviders;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http.Internal;
using Microsoft.AspNet.Mvc.ModelBinding;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;
using Microsoft.AspNet.Routing;
using Microsoft.Framework.Caching;
using Microsoft.Framework.Caching.Memory;
using Moq;
using Xunit;

namespace Microsoft.AspNet.Mvc.TagHelpers
{
    public class ImageTagHelperTest
    {

        [Fact]
        public void PreservesOrderOfSourceAttributesWhenRun()
        {
            // Arrange
            var context = MakeTagHelperContext(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("alt text") },
                    { "data-extra", new HtmlString("something") },                    
                    { "title", new HtmlString("Image title") },
                    { "src", "testimage.png" },
                    { "asp-append-version", "true" }
                });
            var output = MakeImageTagHelperOutput(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("alt text") },
                    { "data-extra", new HtmlString("something") },                    
                    { "title", new HtmlString("Image title") },
                });

            var expectedOutput = MakeImageTagHelperOutput(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("alt text") },
                    { "data-extra", new HtmlString("something") },
                    { "title", new HtmlString("Image title") },
                    { "src", "testimage.png?v=f4OxZX_x_FO5LcGBSKHWXfwtSx-j1ncoSt3SABJtkGk" }
                });

            var hostingEnvironment = MakeHostingEnvironment();
            var viewContext = MakeViewContext();

            var helper = new ImageTagHelper(hostingEnvironment, MakeCache())
            {
                ViewContext = viewContext,
                Src = "testimage.png",
                AppendVersion = true,
            };

            // Act
            helper.Process(context, output);

            // Assert            
            Assert.Equal(expectedOutput.TagName, output.TagName);
            Assert.Equal(4, output.Attributes.Count);
            
            for(int i=0; i < expectedOutput.Attributes.Count; i++)
            {
                var expectedAtribute = expectedOutput.Attributes[i];
                var actualAttribute = output.Attributes[i];
                Assert.Equal(expectedAtribute.Name, actualAttribute.Name);
                Assert.Equal(expectedAtribute.Value.ToString(), actualAttribute.Value.ToString());
            }
        }

        [ConditionalTheory]
        // Mono issue - https://github.com/aspnet/External/issues/21
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        public void RendersImageTag_AddsFileVersion()
        {
            // Arrange
            var context = MakeTagHelperContext(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("Alt image text") },
                    { "src", "/images/test-image.png" },
                    { "asp-append-version", "true" }
                });
            var output = MakeImageTagHelperOutput(attributes: new TagHelperAttributeList
            {
                { "alt", new HtmlString("Alt image text") },
            });
            var hostingEnvironment = MakeHostingEnvironment();
            var viewContext = MakeViewContext();

            var helper = new ImageTagHelper(hostingEnvironment, MakeCache())
            {
                ViewContext = viewContext,
                Src = "/images/test-image.png",
                AppendVersion = true
            };

            // Act
            helper.Process(context, output);

            // Assert
            Assert.True(output.Content.IsEmpty);
            Assert.Equal("img", output.TagName);
            Assert.Equal(2, output.Attributes.Count);
            var srcAttribute = Assert.Single(output.Attributes, attr => attr.Name.Equals("src"));
            Assert.Equal("/images/test-image.png?v=f4OxZX_x_FO5LcGBSKHWXfwtSx-j1ncoSt3SABJtkGk", srcAttribute.Value);
        }

        [Fact]
        public void RendersImageTag_DoesNotAddFileVersion()
        {
            // Arrange
            var context = MakeTagHelperContext(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("Alt image text") },
                    { "src", "/images/test-image.png" },
                    { "asp-append-version", "false" }
                });
            var output = MakeImageTagHelperOutput(attributes: new TagHelperAttributeList
            {
                { "alt", new HtmlString("Alt image text") },
            });
            var hostingEnvironment = MakeHostingEnvironment();
            var viewContext = MakeViewContext();

            var helper = new ImageTagHelper(hostingEnvironment, MakeCache())
            {
                ViewContext = viewContext,
                Src = "/images/test-image.png",
                AppendVersion = false
            };

            // Act
            helper.Process(context, output);

            // Assert
            Assert.True(output.Content.IsEmpty);
            Assert.Equal("img", output.TagName);
            Assert.Equal(2, output.Attributes.Count);
            var srcAttribute = Assert.Single(output.Attributes, attr => attr.Name.Equals("src"));
            Assert.Equal("/images/test-image.png", srcAttribute.Value);
        }

        [ConditionalTheory]
        // Mono issue - https://github.com/aspnet/External/issues/21
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        public void RendersImageTag_AddsFileVersion_WithRequestPathBase()
        {
            // Arrange
            var context = MakeTagHelperContext(
                attributes: new TagHelperAttributeList
                {
                    { "alt", new HtmlString("alt text") },
                    { "src", "/bar/images/image.jpg" },
                    { "asp-append-version", "true" },
                });
            var output = MakeImageTagHelperOutput(attributes: new TagHelperAttributeList
            {
                { "alt", new HtmlString("alt text") },
            });
            var hostingEnvironment = MakeHostingEnvironment();
            var viewContext = MakeViewContext("/bar");

            var helper = new ImageTagHelper(hostingEnvironment, MakeCache())
            {
                ViewContext = viewContext,
                Src = "/bar/images/image.jpg",
                AppendVersion = true
            };

            // Act
            helper.Process(context, output);
            // Assert
            Assert.True(output.Content.IsEmpty);
            Assert.Equal("img", output.TagName);
            Assert.Equal(2, output.Attributes.Count);
            var srcAttribute = Assert.Single(output.Attributes, attr => attr.Name.Equals("src"));
            Assert.Equal("/bar/images/image.jpg?v=f4OxZX_x_FO5LcGBSKHWXfwtSx-j1ncoSt3SABJtkGk", srcAttribute.Value);
        }

        private static ViewContext MakeViewContext(string requestPathBase = null)
        {
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            if (requestPathBase != null)
            {
                actionContext.HttpContext.Request.PathBase = new Http.PathString(requestPathBase);
            }

            var metadataProvider = new EmptyModelMetadataProvider();
            var viewData = new ViewDataDictionary(metadataProvider);
            var viewContext = new ViewContext(
                actionContext,
                Mock.Of<IView>(),
                viewData,
                Mock.Of<ITempDataDictionary>(),
                TextWriter.Null,
                new HtmlHelperOptions());

            return viewContext;
        }

        private static TagHelperContext MakeTagHelperContext(
            TagHelperAttributeList attributes)
        {
            return new TagHelperContext(
                attributes,
                items: new Dictionary<object, object>(),
                uniqueId: Guid.NewGuid().ToString("N"),
                getChildContentAsync: useCachedResult =>
                {
                    var tagHelperContent = new DefaultTagHelperContent();
                    tagHelperContent.SetContent(default(string));
                    return Task.FromResult<TagHelperContent>(tagHelperContent);
                });
        }

        private static TagHelperOutput MakeImageTagHelperOutput(TagHelperAttributeList attributes)
        {
            attributes = attributes ?? new TagHelperAttributeList();

            return new TagHelperOutput("img", attributes);
        }

        private static IHostingEnvironment MakeHostingEnvironment()
        {
            var emptyDirectoryContents = new Mock<IDirectoryContents>();
            emptyDirectoryContents.Setup(dc => dc.GetEnumerator())
                .Returns(Enumerable.Empty<IFileInfo>().GetEnumerator());
            var mockFile = new Mock<IFileInfo>();
            mockFile.SetupGet(f => f.Exists).Returns(true);
            mockFile
                .Setup(m => m.CreateReadStream())
                .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("Hello World!")));
            var mockFileProvider = new Mock<IFileProvider>();
            mockFileProvider.Setup(fp => fp.GetDirectoryContents(It.IsAny<string>()))
                .Returns(emptyDirectoryContents.Object);
            mockFileProvider.Setup(fp => fp.GetFileInfo(It.IsAny<string>()))
                .Returns(mockFile.Object);
            var hostingEnvironment = new Mock<IHostingEnvironment>();
            hostingEnvironment.Setup(h => h.WebRootFileProvider).Returns(mockFileProvider.Object);

            return hostingEnvironment.Object;
        }

        private static IMemoryCache MakeCache()
        {
            object result = null;
            var cache = new Mock<IMemoryCache>();
            cache.CallBase = true;
            cache.Setup(c => c.TryGetValue(It.IsAny<string>(), out result))
                .Returns(result != null);
            cache.Setup(
                    c => c.Set(
                        /*key*/ It.IsAny<string>(),
                        /*value*/ It.IsAny<object>(),
                        /*options*/ It.IsAny<MemoryCacheEntryOptions>()))
                .Returns(new object());
            return cache.Object;
        }
    }
}