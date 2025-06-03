using System.Diagnostics.CodeAnalysis;

namespace DevFlow.SharedKernel.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
public class Result
{
  /// <summary>
  /// Gets a value indicating whether the result is successful.
  /// </summary>
  public bool IsSuccess { get; }

  /// <summary>
  /// Gets a value indicating whether the result is a failure.
  /// </summary>
  public bool IsFailure => !IsSuccess;

  /// <summary>
  /// Gets the error associated with the result.
  /// </summary>
  public Error Error { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="Result"/> class.
  /// </summary>
  protected Result(bool isSuccess, Error error)
  {
    if (isSuccess && error != Error.None ||
        !isSuccess && error == Error.None)
    {
      throw new ArgumentException("Invalid error", nameof(error));
    }

    IsSuccess = isSuccess;
    Error = error;
  }

  /// <summary>
  /// Creates a successful result.
  /// </summary>
  public static Result Success() => new(true, Error.None);

  /// <summary>
  /// Creates a successful result with a value.
  /// </summary>
  public static Result<T> Success<T>(T value) => new(value, true, Error.None);

  /// <summary>
  /// Creates a failed result.
  /// </summary>
  public static Result Failure(Error error) => new(false, error);

  /// <summary>
  /// Creates a failed result with a value type.
  /// </summary>
  public static Result<T> Failure<T>(Error error) => new(default, false, error);

  /// <summary>
  /// Implicit conversion from Error to Result.
  /// </summary>
  public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail.
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public class Result<T> : Result
{
  private readonly T? _value;

  /// <summary>
  /// Gets the value if the result is successful.
  /// </summary>
  public T Value => IsSuccess
      ? _value!
      : throw new InvalidOperationException("Cannot access value of a failed result");

  /// <summary>
  /// Initializes a new instance of the <see cref="Result{T}"/> class.
  /// </summary>
  internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
  {
    _value = value;
  }

  /// <summary>
  /// Creates a successful result with a value.
  /// </summary>
  public static Result<T> Success(T value) => new(value, true, Error.None);

  /// <summary>
  /// Creates a failed result.
  /// </summary>
  public static new Result<T> Failure(Error error) => new(default, false, error);

  /// <summary>
  /// Implicit conversion from T to Result{T}.
  /// </summary>
  public static implicit operator Result<T>(T value) => Success(value);

  /// <summary>
  /// Implicit conversion from Error to Result{T}.
  /// </summary>
  public static implicit operator Result<T>(Error error) => Failure(error);

  /// <summary>
  /// Tries to get the value if the result is successful.
  /// </summary>
  public bool TryGetValue([NotNullWhen(true)] out T? value)
  {
    value = _value;
    return IsSuccess;
  }
}