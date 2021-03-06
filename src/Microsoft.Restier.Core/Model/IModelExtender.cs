﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Restier.Core.Model
{
    /// <summary>
    /// Represents a hook point that extends a model.
    /// </summary>
    /// <remarks>
    /// This is a multi-cast hook point whose instances
    /// are used in the original order of registration.
    /// </remarks>
    public interface IModelExtender
    {
        /// <summary>
        /// Asynchronously extends a model.
        /// </summary>
        /// <param name="context">
        /// The model context.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        Task ExtendModelAsync(
            ModelContext context,
            CancellationToken cancellationToken);
    }
}
