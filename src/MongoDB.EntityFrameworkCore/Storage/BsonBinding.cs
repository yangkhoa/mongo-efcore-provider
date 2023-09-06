﻿/* Copyright 2023-present MongoDB Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MongoDB.EntityFrameworkCore.Storage;

public static class BsonBinding
{
    public static Expression CreateGetValueExpression(Expression bsonDocExpression, IReadOnlyProperty property)
    {
        return CreateGetValueExpression(bsonDocExpression, property.GetElementName(), property.GetTypeMapping().ClrType);
    }

    public static Expression CreateGetValueExpression(Expression bsonDocExpression, string name, Type mappedType)
    {
        if (mappedType.IsArray)
        {
            return CreateGetArrayOf(bsonDocExpression, name, mappedType.TryGetItemType()!);
        }

        // Support lists and variants that expose IEnumerable<T> and have a matching constructor
        if (mappedType is {IsGenericType: true, IsGenericTypeDefinition: false})
        {
            var enumerableType = mappedType.TryFindIEnumerable();
            if (enumerableType != null)
            {
                var constructor = mappedType.TryFindConstructorWithParameter(enumerableType);
                if (constructor != null)
                {
                    return Expression.New(constructor,
                        CreateGetEnumerableOf(bsonDocExpression, name, enumerableType.TryGetItemType()!));
                }
            }
        }

        return CreateGetValueAs(bsonDocExpression, name, mappedType);
    }

    public static Expression CreateGetValueExpression(Expression bsonDocExpression, int index, Type mappedType)
    {
        if (mappedType.IsArray)
        {
            return CreateGetArrayOf(bsonDocExpression, index, mappedType.TryGetItemType()!);
        }

        // Support lists and variants that expose IEnumerable<T> and have a matching constructor
        if (mappedType is {IsGenericType: true, IsGenericTypeDefinition: false})
        {
            var enumerableType = mappedType.TryFindIEnumerable();
            if (enumerableType != null)
            {
                var constructor = mappedType.TryFindConstructorWithParameter(enumerableType);
                if (constructor != null)
                {
                    return Expression.New(constructor,
                        CreateGetEnumerableOf(bsonDocExpression, index, enumerableType.TryGetItemType()!));
                }
            }
        }

        return CreateGetValueAs(bsonDocExpression, index, mappedType);
    }

    public static Expression ConvertTypeIfRequired(Expression expression, Type intendedType)
        => expression.Type != intendedType
            ? Expression.Convert(expression, intendedType)
            : expression;

    private static Expression CreateGetValueAs(Expression bsonValueExpression, string name, Type type) =>
        Expression.Call(null, __getValueAsByNameMethodInfo.MakeGenericMethod(type), bsonValueExpression, Expression.Constant(name));

    private static Expression CreateGetValueAs(Expression bsonValueExpression, int index, Type type) =>
        Expression.Call(null, __getValueAsByIndexMethodInfo.MakeGenericMethod(type), bsonValueExpression,
            Expression.Constant(index));

    private static Expression CreateGetArrayOf(Expression bsonValueExpression, string name, Type type) =>
        Expression.Call(null, __getArrayOfByNameMethodInfo.MakeGenericMethod(type), bsonValueExpression, Expression.Constant(name));

    private static Expression CreateGetArrayOf(Expression bsonValueExpression, int index, Type type) =>
        Expression.Call(null, __getArrayOfByIndexMethodInfo.MakeGenericMethod(type), bsonValueExpression,
            Expression.Constant(index));

    private static Expression CreateGetEnumerableOf(Expression bsonValueExpression, string name, Type type) =>
        Expression.Call(null, __getEnumerableOfByNameMethodInfo.MakeGenericMethod(type), bsonValueExpression,
            Expression.Constant(name));

    private static Expression CreateGetEnumerableOf(Expression bsonValueExpression, int index, Type type) =>
        Expression.Call(null, __getEnumerableOfByIndexMethodInfo.MakeGenericMethod(type), bsonValueExpression,
            Expression.Constant(index));

    private static readonly MethodInfo __getValueAsByNameMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetValueAs) && mi.GetParameters()[1].ParameterType == typeof(string));

    private static readonly MethodInfo __getValueAsByIndexMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetValueAs) && mi.GetParameters()[1].ParameterType == typeof(int));

    private static readonly MethodInfo __getArrayOfByNameMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetArrayOf) && mi.GetParameters()[1].ParameterType == typeof(string));

    private static readonly MethodInfo __getArrayOfByIndexMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetArrayOf) && mi.GetParameters()[1].ParameterType == typeof(int));

    private static readonly MethodInfo __getEnumerableOfByNameMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetEnumerableOf) &&
                          mi.GetParameters()[1].ParameterType == typeof(string));

    private static readonly MethodInfo __getEnumerableOfByIndexMethodInfo
        = typeof(BsonConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(mi => mi.Name == nameof(BsonConverter.GetEnumerableOf) && mi.GetParameters()[1].ParameterType == typeof(int));
}
