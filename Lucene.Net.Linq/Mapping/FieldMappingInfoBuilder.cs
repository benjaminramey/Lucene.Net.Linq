﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Lucene.Net.Linq.Converters;
using Lucene.Net.Linq.Util;
using DateTimeConverter = Lucene.Net.Linq.Converters.DateTimeConverter;

namespace Lucene.Net.Linq.Mapping
{
    internal class FieldMappingInfoBuilder
    {
        internal const string DefaultDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

        internal static IFieldMapper<T> Build<T>(PropertyInfo p)
        {
            var score = p.GetCustomAttribute<QueryScoreAttribute>(true);

            if (score != null)
            {
                return new ReflectionScoreMapper<T>(p);
            }

            var metadata = p.GetCustomAttribute<FieldAttribute>(true);
            var numericFieldAttribute = p.GetCustomAttribute<NumericFieldAttribute>(true);
            var type = p.PropertyType;

            var isCollection = IsCollection(p.PropertyType);

            if (isCollection)
            {
                type = p.PropertyType.GetGenericArguments()[0];
            }

            ReflectionFieldMapper<T> mapper;

            if (numericFieldAttribute != null)
            {
                mapper = NumericFieldMappingInfoBuilder.BuildNumeric<T>(p, type, numericFieldAttribute);
            }
            else
            {
                mapper = BuildPrimitive<T>(p, type, metadata);
            }

            return isCollection ? new CollectionReflectionFieldMapper<T>(mapper, type) : mapper;
        }

        private static bool IsCollection(Type type)
        {
            return type.IsGenericType &&
                   typeof (IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition());
        }

        private static ReflectionFieldMapper<T> BuildPrimitive<T>(PropertyInfo p, Type type, FieldAttribute metadata)
        {
            var fieldName = (metadata != null ? metadata.Field : null) ?? p.Name;
            var converter = GetConverter(p, type, metadata);
            var store = metadata != null ? metadata.Store : StoreMode.Yes;
            var index = metadata != null ? metadata.IndexMode : IndexMode.Analyzed;
            var caseSensitive = GetCaseSensitivity(metadata);

            return new ReflectionFieldMapper<T>(p, store, index, converter, fieldName, caseSensitive);
        }

        internal static bool GetCaseSensitivity(FieldAttribute metadata)
        {
            if (metadata == null) return false;

            return metadata.CaseSensitive ||
                   metadata.IndexMode == IndexMode.NotAnalyzed ||
                   metadata.IndexMode == IndexMode.NotAnalyzedNoNorms;
        }

        internal static TypeConverter GetConverter(PropertyInfo p, Type type, FieldAttribute metadata)
        {
            if (metadata != null && metadata.Converter != null)
            {
                return (TypeConverter)Activator.CreateInstance(metadata.Converter);
            }

            var formatSpecified = metadata != null && metadata.Format != null;
            var format = (metadata != null ? metadata.Format : null) ?? DefaultDateTimeFormat;
            var propType = p.PropertyType.GetUnderlyingType();
            
            if (propType == typeof(DateTime))
            {
                return new DateTimeConverter(format);
            }
            
            if (formatSpecified || propType == typeof(DateTimeOffset))
            {
                return new FormatConverter(propType, format);
            }

            if (p.PropertyType == typeof(string)) return null;

            var converter = TypeDescriptor.GetConverter(type);

            if (converter == null || !converter.CanConvertFrom(typeof(string)))
            {
                throw new NotSupportedException("Property " + p.Name + " of type " + p.PropertyType + " cannot be converted from " + typeof(string));
            }
            return converter;
        }
    }
}