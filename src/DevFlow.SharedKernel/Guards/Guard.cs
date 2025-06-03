using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DevFlow.SharedKernel.Guards;

/// <summary>
/// Provides guard clauses for defensive programming.
/// </summary>
public static class Guard
{
  /// <summary>
  /// Throws an <see cref="ArgumentNullException"/> if the argument is null.
  /// </summary>
  /// <typeparam name="T">The type of the argument</typeparam>
  /// <param name="argument">The argument to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not null</returns>
  public static T NotNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
      where T : class
  {
    if (argument is null)
    {
      throw new ArgumentNullException(parameterName);
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentNullException"/> if the argument is null.
  /// </summary>
  /// <typeparam name="T">The type of the argument</typeparam>
  /// <param name="argument">The argument to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not null</returns>
  public static T NotNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
      where T : struct
  {
    if (argument is null)
    {
      throw new ArgumentNullException(parameterName);
    }

    return argument.Value;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the string is null or empty.
  /// </summary>
  /// <param name="argument">The string argument to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not null or empty</returns>
  public static string NotNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (string.IsNullOrEmpty(argument))
    {
      throw new ArgumentException("String cannot be null or empty.", parameterName);
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the string is null, empty, or whitespace.
  /// </summary>
  /// <param name="argument">The string argument to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not null, empty, or whitespace</returns>
  public static string NotNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (string.IsNullOrWhiteSpace(argument))
    {
      throw new ArgumentException("String cannot be null, empty, or whitespace.", parameterName);
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the collection is null or empty.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="argument">The collection argument to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not null or empty</returns>
  public static IEnumerable<T> NotNullOrEmpty<T>([NotNull] IEnumerable<T>? argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    NotNull(argument, parameterName);

    if (!argument.Any())
    {
      throw new ArgumentException("Collection cannot be empty.", parameterName);
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentOutOfRangeException"/> if the value is not within the specified range.
  /// </summary>
  /// <typeparam name="T">The type of the value</typeparam>
  /// <param name="argument">The value to check</param>
  /// <param name="min">The minimum value (inclusive)</param>
  /// <param name="max">The maximum value (inclusive)</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is within the range</returns>
  public static T InRange<T>(T argument, T min, T max, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
      where T : IComparable<T>
  {
    if (argument.CompareTo(min) < 0 || argument.CompareTo(max) > 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, argument, $"Value must be between {min} and {max}.");
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the condition is false.
  /// </summary>
  /// <param name="condition">The condition to check</param>
  /// <param name="message">The error message</param>
  /// <param name="parameterName">The name of the parameter</param>
  public static void Against(bool condition, string message, [CallerArgumentExpression(nameof(condition))] string? parameterName = null)
  {
    if (condition)
    {
      throw new ArgumentException(message, parameterName);
    }
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the condition is true.
  /// </summary>
  /// <param name="condition">The condition to check</param>
  /// <param name="message">The error message</param>
  /// <param name="parameterName">The name of the parameter</param>
  public static void Requires(bool condition, string message, [CallerArgumentExpression(nameof(condition))] string? parameterName = null)
  {
    if (!condition)
    {
      throw new ArgumentException(message, parameterName);
    }
  }

  /// <summary>
  /// Throws an <see cref="ArgumentOutOfRangeException"/> if the value is negative.
  /// </summary>
  /// <param name="argument">The value to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not negative</returns>
  public static int NotNegative(int argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (argument < 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, argument, "Value cannot be negative.");
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentOutOfRangeException"/> if the value is negative.
  /// </summary>
  /// <param name="argument">The value to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not negative</returns>
  public static decimal NotNegative(decimal argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (argument < 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, argument, "Value cannot be negative.");
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentOutOfRangeException"/> if the value is negative or zero.
  /// </summary>
  /// <param name="argument">The value to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is positive</returns>
  public static int Positive(int argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (argument <= 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, argument, "Value must be positive.");
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentOutOfRangeException"/> if the value is negative or zero.
  /// </summary>
  /// <param name="argument">The value to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is positive</returns>
  public static decimal Positive(decimal argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (argument <= 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, argument, "Value must be positive.");
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the Guid is empty.
  /// </summary>
  /// <param name="argument">The Guid to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not empty</returns>
  public static Guid NotEmpty(Guid argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (argument == Guid.Empty)
    {
      throw new ArgumentException("Guid cannot be empty.", parameterName);
    }

    return argument;
  }

  /// <summary>
  /// Throws an <see cref="ArgumentException"/> if the value is the default value for its type.
  /// </summary>
  /// <typeparam name="T">The type of the value</typeparam>
  /// <param name="argument">The value to check</param>
  /// <param name="parameterName">The name of the parameter</param>
  /// <returns>The argument if it is not the default value</returns>
  public static T NotDefault<T>(T argument, [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
  {
    if (EqualityComparer<T>.Default.Equals(argument, default(T)!))
    {
      throw new ArgumentException($"Value cannot be the default value for type {typeof(T).Name}.", parameterName);
    }

    return argument;
  }
}