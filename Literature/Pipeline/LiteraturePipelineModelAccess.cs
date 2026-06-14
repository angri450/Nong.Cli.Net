using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Pipeline;

internal static class LiteraturePipelineModelAccess
{
    public static T Create<T>() where T : class => (T)Create(typeof(T));

    public static object Create(Type type)
    {
        var parameterless = type.GetConstructor(Type.EmptyTypes);
        if (parameterless is not null)
        {
            return parameterless.Invoke(null);
        }

        return RuntimeHelpers.GetUninitializedObject(type);
    }

    public static T CreateWithValues<T>(IReadOnlyDictionary<string, object?> values) where T : class
    {
        var type = typeof(T);
        var instance = TryCreateFromConstructor(type, values) ?? Create(type);

        foreach (var pair in values)
        {
            TrySet(instance, pair.Value, pair.Key);
        }

        return (T)instance;
    }

    public static LiteratureIssue Issue(
        string severity,
        string code,
        string message,
        string? provider = null,
        string? field = null)
    {
        return CreateWithValues<LiteratureIssue>(new Dictionary<string, object?>
        {
            ["Id"] = code,
            ["Severity"] = severity,
            ["Level"] = severity,
            ["Code"] = code,
            ["Message"] = message,
            ["Provider"] = provider,
            ["Field"] = field
        });
    }

    public static object? Get(object? model, params string[] propertyNames)
    {
        if (model is null)
        {
            return null;
        }

        var type = model.GetType();
        foreach (var name in propertyNames)
        {
            var property = FindProperty(type, name);
            if (property is not null)
            {
                return property.GetValue(model);
            }
        }

        return null;
    }

    public static bool TrySet(object? model, object? value, params string[] propertyNames)
    {
        if (model is null)
        {
            return false;
        }

        var type = model.GetType();
        foreach (var name in propertyNames)
        {
            var property = FindProperty(type, name);
            if (property is null || !property.CanWrite)
            {
                continue;
            }

            try
            {
                property.SetValue(model, ConvertValue(value, property.PropertyType));
                return true;
            }
            catch
            {
                // Continue probing alternate property names. The model surface is shared
                // with other Stage19 work, so property naming can differ slightly.
            }
        }

        return false;
    }

    public static string? String(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        return value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text,
            _ => value.ToString()
        };
    }

    public static IReadOnlyList<string> Strings(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        return ToStringList(value);
    }

    public static IReadOnlyList<string> ToStringList(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : new[] { text };
        }

        if (value is IEnumerable enumerable)
        {
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                var itemText = item?.ToString();
                if (!string.IsNullOrWhiteSpace(itemText))
                {
                    result.Add(itemText);
                }
            }

            return result;
        }

        var scalar = value.ToString();
        return string.IsNullOrWhiteSpace(scalar) ? Array.Empty<string>() : new[] { scalar };
    }

    public static int? Int(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        if (value is null)
        {
            return null;
        }

        if (value is int number)
        {
            return number;
        }

        if (value is long longNumber)
        {
            return longNumber > int.MaxValue || longNumber < int.MinValue ? null : (int)longNumber;
        }

        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    public static double? Double(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        if (value is null)
        {
            return null;
        }

        if (value is double number)
        {
            return number;
        }

        if (value is float floatNumber)
        {
            return floatNumber;
        }

        if (value is decimal decimalNumber)
        {
            return (double)decimalNumber;
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    public static IReadOnlyDictionary<string, string> Dictionary(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        if (value is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is IEnumerable enumerable)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                var itemType = item.GetType();
                var key = GetPairPart(item, itemType, "Key")?.ToString();
                var itemValue = GetPairPart(item, itemType, "Value")?.ToString();
                if (!string.IsNullOrWhiteSpace(key) && itemValue is not null)
                {
                    result[key] = itemValue;
                }
            }

            return result;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsBlank(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    public static IReadOnlyList<object> Objects(object? model, params string[] propertyNames)
    {
        var value = Get(model, propertyNames);
        if (value is null || value is string)
        {
            return Array.Empty<object>();
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Where(item => item is not null).ToArray();
        }

        return new[] { value };
    }

    public static bool ContainsSecretLikeValue(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return text.Contains('@', StringComparison.Ordinal) ||
                text.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("token", StringComparison.OrdinalIgnoreCase);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (ContainsSecretLikeValue(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static object? TryCreateFromConstructor(Type type, IReadOnlyDictionary<string, object?> values)
    {
        foreach (var constructor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length == 0)
            {
                continue;
            }

            var args = new object?[parameters.Length];
            var matched = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var value = values
                    .FirstOrDefault(pair => string.Equals(pair.Key, parameter.Name, StringComparison.OrdinalIgnoreCase));

                if (value.Key is null)
                {
                    if (parameter.HasDefaultValue)
                    {
                        args[i] = parameter.DefaultValue;
                        continue;
                    }

                    matched = false;
                    break;
                }

                args[i] = ConvertValue(value.Value, parameter.ParameterType);
            }

            if (!matched)
            {
                continue;
            }

            try
            {
                return constructor.Invoke(args);
            }
            catch
            {
                // Fall back to property setting below.
            }
        }

        return null;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return Nullable.GetUnderlyingType(targetType) is not null || !targetType.IsValueType
                ? null
                : Activator.CreateInstance(targetType);
        }

        var nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable is not null)
        {
            return ConvertValue(value, nullable);
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        if (targetType == typeof(bool))
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(value.ToString(), out var parsedBool) && parsedBool;
        }

        if (targetType.IsEnum)
        {
            return value is string enumName
                ? Enum.Parse(targetType, enumName, ignoreCase: true)
                : Enum.ToObject(targetType, value);
        }

        if (targetType == typeof(int))
        {
            return value is int ? value : Convert.ToInt32(value);
        }

        if (targetType == typeof(double))
        {
            return value is double ? value : Convert.ToDouble(value);
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType() ?? typeof(object);
            var values = ToEnumerable(value).Select(item => ConvertValue(item, elementType)).ToArray();
            var array = Array.CreateInstance(elementType, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        if (IsDictionaryType(targetType))
        {
            return ConvertDictionary(value, targetType);
        }

        if (IsEnumerableType(targetType, out var itemType))
        {
            var listType = typeof(List<>).MakeGenericType(itemType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in ToEnumerable(value))
            {
                list.Add(ConvertValue(item, itemType));
            }

            return list;
        }

        return value;
    }

    private static object ConvertDictionary(object value, Type targetType)
    {
        var args = targetType.GetGenericArguments();
        var keyType = args.Length >= 1 ? args[0] : typeof(string);
        var valueType = args.Length >= 2 ? args[1] : typeof(string);
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                var itemType = item.GetType();
                var key = GetPairPart(item, itemType, "Key");
                var itemValue = GetPairPart(item, itemType, "Value");
                if (key is not null)
                {
                    dictionary[ConvertValue(key, keyType)!] = ConvertValue(itemValue, valueType);
                }
            }
        }

        return dictionary;
    }

    private static IEnumerable<object?> ToEnumerable(object value)
    {
        if (value is string)
        {
            yield return value;
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        yield return value;
    }

    private static bool IsEnumerableType(Type type, out Type itemType)
    {
        itemType = typeof(object);
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ||
             type.GetGenericTypeDefinition() == typeof(IList<>) ||
             type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            itemType = type.GetGenericArguments()[0];
            return true;
        }

        var enumerable = type
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is not null)
        {
            itemType = enumerable.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    private static bool IsDictionaryType(Type type)
    {
        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
             type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) ||
             type.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
        {
            return true;
        }

        return type
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    }

    private static object? GetPairPart(object pair, Type pairType, string name)
    {
        return pairType.GetProperty(name)?.GetValue(pair) ??
            pairType.GetField(name)?.GetValue(pair);
    }
}
