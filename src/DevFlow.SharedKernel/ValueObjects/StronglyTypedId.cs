using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.SharedKernel.ValueObjects;

/// <summary>
/// Base class for strongly-typed identifiers.
/// </summary>
/// <typeparam name="TValue">The underlying value type</typeparam>
public abstract class StronglyTypedId<TValue> : ValueObject
    where TValue : notnull
{
  /// <summary>
  /// Gets the underlying value of the identifier.
  /// </summary>
  public TValue Value { get; }

  /// <summary>
  /// Initializes a new instance of the strongly-typed identifier.
  /// </summary>
  /// <param name="value">The underlying value</param>
  protected StronglyTypedId(TValue value)
  {
    Value = value ?? throw new ArgumentNullException(nameof(value));
  }

  /// <summary>
  /// Gets the equality components for this identifier.
  /// </summary>
  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  /// <summary>
  /// Returns the string representation of the identifier.
  /// </summary>
  public override string ToString() => Value.ToString() ?? string.Empty;

  /// <summary>
  /// Implicit conversion to the underlying value type.
  /// </summary>
  public static implicit operator TValue(StronglyTypedId<TValue> id) => id.Value;
}

/// <summary>
/// Base class for strongly-typed identifiers with Guid values.
/// </summary>
public abstract class StronglyTypedId : StronglyTypedId<Guid>
{
  /// <summary>
  /// Initializes a new instance of the strongly-typed identifier.
  /// </summary>
  /// <param name="value">The underlying Guid value</param>
  protected StronglyTypedId(Guid value) : base(value)
  {
    if (value == Guid.Empty)
      throw new ArgumentException("Identifier cannot be empty", nameof(value));
  }

  /// <summary>
  /// Creates a new identifier with a new Guid value.
  /// </summary>
  protected static TId New<TId>() where TId : StronglyTypedId
  {
    return (TId)Activator.CreateInstance(typeof(TId), Guid.NewGuid())!;
  }

  /// <summary>
  /// Creates an identifier from the specified Guid value.
  /// </summary>
  protected static TId From<TId>(Guid value) where TId : StronglyTypedId
  {
    return (TId)Activator.CreateInstance(typeof(TId), value)!;
  }

  /// <summary>
  /// Creates an identifier from the specified string value.
  /// </summary>
  protected static TId From<TId>(string value) where TId : StronglyTypedId
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Identifier string cannot be null or empty", nameof(value));

    if (!Guid.TryParse(value, out var guid))
      throw new ArgumentException("Invalid Guid format", nameof(value));

    return From<TId>(guid);
  }
}

/// <summary>
/// Type converter for strongly-typed identifiers.
/// </summary>
public class StronglyTypedIdConverter<TId> : TypeConverter
    where TId : StronglyTypedId
{
  public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
  {
    return sourceType == typeof(string) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);
  }

  public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
  {
    return value switch
    {
      string stringValue => Activator.CreateInstance(typeof(TId), Guid.Parse(stringValue)),
      Guid guidValue => Activator.CreateInstance(typeof(TId), guidValue),
      _ => base.ConvertFrom(context, culture, value)
    };
  }

  public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
  {
    return destinationType == typeof(string) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);
  }

  public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
  {
    if (value is TId id)
    {
      if (destinationType == typeof(string))
        return id.Value.ToString();
      if (destinationType == typeof(Guid))
        return id.Value;
    }

    return base.ConvertTo(context, culture, value, destinationType);
  }
}

/// <summary>
/// JSON converter for strongly-typed identifiers.
/// </summary>
public class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : StronglyTypedId
{
  public override TId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
      return null;

    if (reader.TokenType == JsonTokenType.String)
    {
      var stringValue = reader.GetString();
      if (string.IsNullOrEmpty(stringValue))
        return null;

      if (Guid.TryParse(stringValue, out var guid))
        return (TId)Activator.CreateInstance(typeof(TId), guid)!;
    }

    throw new JsonException($"Unable to convert JSON to {typeof(TId).Name}");
  }

  public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
  {
    if (value is null)
      writer.WriteNullValue();
    else
      writer.WriteStringValue(value.Value.ToString());
  }
}