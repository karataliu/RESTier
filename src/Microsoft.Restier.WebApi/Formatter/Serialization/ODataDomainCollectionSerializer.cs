﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Web.OData.Formatter.Serialization;
using Microsoft.OData.Core;
using Microsoft.Restier.WebApi.Results;

namespace Microsoft.Restier.WebApi.Formatter.Serialization
{
    /// <summary>
    /// The serializer for collection result.
    /// </summary>
    public class ODataDomainCollectionSerializer : ODataCollectionSerializer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ODataDomainCollectionSerializer" /> class.
        /// </summary>
        /// <param name="provider">The serializer provider.</param>
        public ODataDomainCollectionSerializer(ODataSerializerProvider provider)
            : base(provider)
        {
        }

        /// <summary>
        /// Writes the complex result to the response message.
        /// </summary>
        /// <param name="graph">The collection result to write.</param>
        /// <param name="type">The type of the collection.</param>
        /// <param name="messageWriter">The message writer.</param>
        /// <param name="writeContext">The writing context.</param>
        public override void WriteObject(
            object graph,
            Type type,
            ODataMessageWriter messageWriter,
            ODataSerializerContext writeContext)
        {
            ValueCollectionResult collectionResult = graph as ValueCollectionResult;
            if (collectionResult != null)
            {
                graph = collectionResult.Query;
            }

            base.WriteObject(graph, type, messageWriter, writeContext);
        }
    }
}