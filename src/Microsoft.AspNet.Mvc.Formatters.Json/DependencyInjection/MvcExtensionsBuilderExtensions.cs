// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Internal;
using Microsoft.Framework.OptionsModel;

namespace Microsoft.Framework.DependencyInjection
{
    public static class MvcExtensionsBuilderExtensions
    {
        public static IMvcBuilder AddJsonFormatters([NotNull] this IMvcBuilder builder)
        {
            builder.Services.AddTransient<IConfigureOptions<MvcOptions>, JsonMvcOptionsSetup>();
            return builder;
        }

        public static IMvcBuilder AddJsonFormatters(
            [NotNull] this IMvcBuilder builder,
            [NotNull] Action<MvcJsonOptions> setupAction)
        {
            AddJsonFormatters(builder);
            ConfigureJsonFormatters(builder, setupAction);
            return builder;
        }

        public static IMvcBuilder ConfigureJsonFormatters(
            [NotNull] this IMvcBuilder builder,
            [NotNull] Action<MvcJsonOptions> setupAction)
        {
            builder.Services.Configure(setupAction);
            return builder;
        }
    }
}
