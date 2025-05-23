using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Base class for strongly-typed identifiers.
/// Provides type safety, serialization support, and validation.
/// </summary>
/// <typeparam name="T">The derived type</typeparam>
[JsonConverter(typeof(StronglyTypedIdJsonConverter))]
public abstract class StronglyTypedId<T> : ValueObject, IEntityId
    where T : StronglyTypedId<T>
{
    protected StronglyTypedId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ID value cannot be null or whitespace.", nameof(value));
            
        Value = value;
    }

    /// <summary>
    /// Gets the string value of the identifier.
    /// </summary>
    public string Value { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public bool Equals(IEntityId? other)
    {
        return other is StronglyTypedId<T> stronglyTyped && Equals(stronglyTyped);
    }

    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts the strongly-typed ID to its string value.
    /// </summary>
    public static implicit operator string(StronglyTypedId<T> id) => id.Value;
}

/// <summary>
/// JSON converter for strongly-typed IDs.
/// </summary>
public class StronglyTypedIdJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && 
               typeToConvert.GetGenericTypeDefinition() == typeof(StronglyTypedId<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedIdJsonConverterInner<>)
            .MakeGenericType(typeToConvert);
        
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private class StronglyTypedIdJsonConverterInner<T> : JsonConverter<T>
        where T : StronglyTypedId<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value is null)
                throw new JsonException($"Cannot deserialize null value to {typeToConvert.Name}");

            return (T)Activator.CreateInstance(typeToConvert, value)!;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}