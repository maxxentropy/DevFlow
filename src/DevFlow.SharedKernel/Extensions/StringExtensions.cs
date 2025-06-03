using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DevFlow.SharedKernel.Extensions;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExtensions
{
  /// <summary>
  /// Converts a string to camelCase.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The camelCase string</returns>
  public static string ToCamelCase(this string input)
  {
    if (string.IsNullOrEmpty(input) || !char.IsUpper(input[0]))
      return input;

    var chars = input.ToCharArray();
    for (var i = 0; i < chars.Length; i++)
    {
      if (i == 1 && !char.IsUpper(chars[i]))
        break;

      var hasNext = i + 1 < chars.Length;
      if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
        break;

      chars[i] = char.ToLowerInvariant(chars[i]);
    }

    return new string(chars);
  }

  /// <summary>
  /// Converts a string to PascalCase.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The PascalCase string</returns>
  public static string ToPascalCase(this string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    if (input.Length == 1)
      return input.ToUpperInvariant();

    return char.ToUpperInvariant(input[0]) + input.Substring(1);
  }

  /// <summary>
  /// Converts a string to snake_case.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The snake_case string</returns>
  public static string ToSnakeCase(this string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    var result = new StringBuilder();
    var previousChar = char.MinValue;

    for (var i = 0; i < input.Length; i++)
    {
      var currentChar = input[i];

      if (i > 0 && char.IsUpper(currentChar) &&
          (char.IsLower(previousChar) || (i < input.Length - 1 && char.IsLower(input[i + 1]))))
      {
        result.Append('_');
      }

      result.Append(char.ToLowerInvariant(currentChar));
      previousChar = currentChar;
    }

    return result.ToString();
  }

  /// <summary>
  /// Converts a string to kebab-case.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The kebab-case string</returns>
  public static string ToKebabCase(this string input)
  {
    return ToSnakeCase(input).Replace('_', '-');
  }

  /// <summary>
  /// Truncates a string to the specified length.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <param name="maxLength">The maximum length</param>
  /// <param name="suffix">The suffix to append if truncated</param>
  /// <returns>The truncated string</returns>
  public static string Truncate(this string input, int maxLength, string suffix = "...")
  {
    if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
      return input;

    return input.Substring(0, maxLength - suffix.Length) + suffix;
  }

  /// <summary>
  /// Removes all whitespace from a string.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The string without whitespace</returns>
  public static string RemoveWhitespace(this string input)
  {
    return string.IsNullOrEmpty(input) ? input : Regex.Replace(input, @"\s+", "");
  }

  /// <summary>
  /// Checks if a string is a valid email address.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>True if the string is a valid email address</returns>
  public static bool IsValidEmail(this string input)
  {
    if (string.IsNullOrWhiteSpace(input))
      return false;

    const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
  }

  /// <summary>
  /// Converts a string to title case.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The title case string</returns>
  public static string ToTitleCase(this string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
  }

  /// <summary>
  /// Checks if a string contains only alphanumeric characters.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>True if the string contains only alphanumeric characters</returns>
  public static bool IsAlphanumeric(this string input)
  {
    return !string.IsNullOrEmpty(input) && input.All(char.IsLetterOrDigit);
  }

  /// <summary>
  /// Reverses a string.
  /// </summary>
  /// <param name="input">The input string</param>
  /// <returns>The reversed string</returns>
  public static string Reverse(this string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return new string(input.Reverse().ToArray());
  }
}