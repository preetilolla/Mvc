// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Internal;

namespace Microsoft.Framework.DependencyInjection
{
    public static class MvcCoreBuilderExtensions
    {
        public static IMvcBuilder Configure(
            [NotNull] this IMvcBuilder builder,
            [NotNull] Action<MvcOptions> setupAction)
        {
            builder.Services.Configure(setupAction);
            return builder;
        }
    }
}
