﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AnnotationInspector.cs">
//   Copyright (c) by respective owners including Yahoo!, Microsoft, and
//   individual contributors. All rights reserved.  Released under a BSD
//   license as described in the file LICENSE.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VW.Serializer.Attributes;
using VW.Serializer.Intermediate;

namespace VW.Serializer
{
    internal static class AnnotationInspector
    {
        internal static List<FeatureExpression> ExtractFeatures(Type type, Func<PropertyInfo, FeatureAttribute, bool> propertyPredicate)
        {
            Contract.Requires(type != null);
            Contract.Requires(propertyPredicate != null);

            var validExpressions = new Stack<Func<Expression,Expression>>();

            // CODE example != null
            validExpressions.Push(valueExpression => Expression.NotEqual(valueExpression, Expression.Constant(null)));

            return ExtractFeatures(
                type,
                null,
                null,
                null,
                // CODE example
                valueExpression => valueExpression,
                validExpressions,
                propertyPredicate);
        }

        private static List<FeatureExpression> ExtractFeatures(
            Type type,
            string parentNamespace,
            char? parentFeatureGroup,
            bool? parentDictify,
            Func<Expression, Expression> valueExpressionFactory,
            Stack<Func<Expression, Expression>> valueValidExpressionFactories,
            Func<PropertyInfo, FeatureAttribute, bool> propertyPredicate)
        {
            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public);

            var localFeatures = from p in props
                                let declaredAttr = (FeatureAttribute)p.GetCustomAttributes(typeof(FeatureAttribute), true).FirstOrDefault()
                                where propertyPredicate(p, declaredAttr)
                                let attr = declaredAttr ?? new FeatureAttribute()
                                select new FeatureExpression(
                                    featureType: p.PropertyType,
                                    name: attr.Name ?? p.Name,
                                    // CODE example.Property
                                    valueExpressionFactory: valueExpression => Expression.Property(valueExpressionFactory(valueExpression), p),
                                    // @Reverse: make sure conditions are specified in the right order
                                    valueValidExpressionFactories: valueValidExpressionFactories.Reverse().ToList(),
                                    @namespace: attr.Namespace ?? parentNamespace,
                                    featureGroup: attr.InternalFeatureGroup ?? parentFeatureGroup,
                                    enumerize: attr.Enumerize,
                                    variableName: p.Name,
                                    order: attr.Order,
                                    addAnchor: attr.AddAnchor,
                                    stringProcessing: attr.StringProcessing,
                                    dictify: attr.InternalDictify ?? parentDictify);

            // Recurse
            return localFeatures
                .SelectMany(f =>
                {
                    // CODE example.Prop1.Prop2 != null
                    valueValidExpressionFactories.Push(valueExpression => Expression.NotEqual(f.ValueExpressionFactory(valueExpression), Expression.Constant(null)));
                    var subFeatures = ExtractFeatures(f.FeatureType, f.Namespace, f.FeatureGroup, f.Dictify, f.ValueExpressionFactory, valueValidExpressionFactories, propertyPredicate);
                    valueValidExpressionFactories.Pop();

                    return subFeatures.Count == 0 ? new List<FeatureExpression>{ f } : subFeatures;
                })
                .ToList();
        }
    }
}
