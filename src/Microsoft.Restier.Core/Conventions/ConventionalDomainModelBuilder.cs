﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.Restier.Core.Model;
using Microsoft.Restier.Core.Query;

namespace Microsoft.Restier.Core.Conventions
{
    /// <summary>
    /// A conventional domain model builder that extends a model, maps between
    /// the model space and the object space, and expands a query expression.
    /// </summary>
    internal class ConventionalDomainModelBuilder :
        IModelBuilder, IDelegateHookHandler<IModelBuilder>,
        IModelMapper, IDelegateHookHandler<IModelMapper>,
        IQueryExpressionExpander
    {
        private readonly Type targetType;
        private readonly ICollection<PropertyInfo> publicProperties = new List<PropertyInfo>();
        private readonly ICollection<PropertyInfo> entitySetProperties = new List<PropertyInfo>();
        private readonly ICollection<PropertyInfo> singletonProperties = new List<PropertyInfo>();

        private ConventionalDomainModelBuilder(Type targetType)
        {
            this.targetType = targetType;
        }

        IModelBuilder IDelegateHookHandler<IModelBuilder>.InnerHandler { get; set; }

        IModelMapper IDelegateHookHandler<IModelMapper>.InnerHandler { get; set; }

        /// <inheritdoc/>
        public static void ApplyTo(
            DomainConfiguration configuration,
            Type targetType)
        {
            Ensure.NotNull(configuration, "configuration");
            Ensure.NotNull(targetType, "targetType");

            var provider = new ConventionalDomainModelBuilder(targetType);
            configuration.AddHookHandler<IModelBuilder>(provider);
            configuration.AddHookHandler<IModelMapper>(provider);
            configuration.AddHookHandler<IQueryExpressionExpander>(provider);
        }

        /// <inheritdoc/>
        public async Task<IEdmModel> GetModelAsync(InvocationContext context, CancellationToken cancellationToken)
        {
            Ensure.NotNull(context);

            IEdmModel modelReturned = await GetModelReturnedByInnerHandlerAsync(context, cancellationToken);
            if (modelReturned == null)
            {
                // There is no model returned so return an empty model.
                var emptyModel = new EdmModel();
                emptyModel.EnsureEntityContainer(this.targetType);
                return emptyModel;
            }

            EdmModel edmModel = modelReturned as EdmModel;
            if (edmModel == null)
            {
                // The model returned is not an EDM model.
                return modelReturned;
            }

            this.ScanForDeclaredPublicProperties();
            this.BuildEntitySetsAndSingletons(context, edmModel);
            return edmModel;
        }

        /// <inheritdoc/>
        public bool TryGetRelevantType(
            DomainContext context,
            string name,
            out Type relevantType)
        {
            relevantType = null;
            var entitySetProperty = this.entitySetProperties.SingleOrDefault(p => p.Name == name);
            if (entitySetProperty != null)
            {
                relevantType = entitySetProperty.PropertyType.GetGenericArguments()[0];
            }

            if (relevantType != null)
            {
                return true;
            }

            var innerHandler = ((IDelegateHookHandler<IModelMapper>)this).InnerHandler;
            return innerHandler != null && innerHandler.TryGetRelevantType(context, name, out relevantType);
        }

        /// <inheritdoc/>
        public bool TryGetRelevantType(
            DomainContext context,
            string namespaceName,
            string name,
            out Type relevantType)
        {
            relevantType = null;
            return false;
        }

        /// <inheritdoc/>
        public Expression Expand(QueryExpressionContext context)
        {
            Ensure.NotNull(context);
            if (context.ModelReference == null)
            {
                return null;
            }

            var domainDataReference = context.ModelReference as DomainDataReference;
            if (domainDataReference == null)
            {
                return null;
            }

            var entitySet = domainDataReference.Element as IEdmEntitySet;
            if (entitySet == null)
            {
                return null;
            }

            var entitySetProperty = this.entitySetProperties
                .SingleOrDefault(p => p.Name == entitySet.Name);
            if (entitySetProperty != null)
            {
                object target = null;
                if (!entitySetProperty.GetMethod.IsStatic)
                {
                    target = context.QueryContext.DomainContext
                        .GetProperty(typeof(Domain).AssemblyQualifiedName);
                    if (target == null ||
                        !this.targetType.IsAssignableFrom(target.GetType()))
                    {
                        return null;
                    }
                }

                var result = entitySetProperty.GetValue(target) as IQueryable;
                if (result != null)
                {
                    return result.Expression;
                }
            }

            return null;
        }

        private static bool IsEntitySetProperty(PropertyInfo property)
        {
            return property.PropertyType.IsGenericType &&
                   property.PropertyType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                   property.PropertyType.GetGenericArguments()[0].IsClass;
        }

        private static bool IsSingletonProperty(PropertyInfo property)
        {
            return !property.PropertyType.IsGenericType && property.PropertyType.IsClass;
        }

        private async Task<IEdmModel> GetModelReturnedByInnerHandlerAsync(
            InvocationContext context, CancellationToken cancellationToken)
        {
            var innerHandler = ((IDelegateHookHandler<IModelBuilder>)this).InnerHandler;
            if (innerHandler != null)
            {
                return await innerHandler.GetModelAsync(context, cancellationToken);
            }

            return null;
        }

        private void ScanForDeclaredPublicProperties()
        {
            var currentType = this.targetType;
            while (currentType != null && currentType != typeof(DomainBase))
            {
                var publicPropertiesDeclaredOnCurrentType = currentType.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly);

                foreach (var property in publicPropertiesDeclaredOnCurrentType)
                {
                    if (property.CanRead &&
                        publicProperties.All(p => p.Name != property.Name))
                    {
                        publicProperties.Add(property);
                    }
                }

                currentType = currentType.BaseType;
            }
        }

        private void BuildEntitySetsAndSingletons(InvocationContext context, EdmModel model)
        {
            var configuration = context.DomainContext.Configuration;
            foreach (var property in this.publicProperties)
            {
                if (configuration.IsPropertyIgnored(property.Name))
                {
                    continue;
                }

                var isEntitySet = IsEntitySetProperty(property);
                if (!isEntitySet)
                {
                    if (!IsSingletonProperty(property))
                    {
                        continue;
                    }
                }

                var propertyType = property.PropertyType;
                if (isEntitySet)
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                var entityType = model.FindDeclaredType(propertyType.FullName) as IEdmEntityType;
                if (entityType == null)
                {
                    // Skip property whose entity type has not been declared yet.
                    continue;
                }

                var container = model.EnsureEntityContainer(this.targetType);
                if (isEntitySet)
                {
                    if (container.FindEntitySet(property.Name) == null)
                    {
                        this.entitySetProperties.Add(property);
                        container.AddEntitySet(property.Name, entityType);
                    }
                }
                else
                {
                    if (container.FindSingleton(property.Name) == null)
                    {
                        this.singletonProperties.Add(property);
                        container.AddSingleton(property.Name, entityType);
                    }
                }
            }
        }
    }
}