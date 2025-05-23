// File: src/DevFlow.SharedKernel/Common/Result.cs
using System.Diagnostics.CodeAnalysis;

namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// Provides a way to handle errors without throwing exceptions.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly bool _isSuccess;
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the error if the operation failed, otherwise null.
    /// </summary>
    public Error? Error => _error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result Failure(string errorMessage) => new(false, Error.Failure(errorMessage));

    /// <summary>
    /// Executes the specified action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes the specified action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    public bool Equals(Result other) => _isSuccess == other._isSuccess && Equals(_error, other._error);
    public override bool Equals(object? obj) => obj is Result other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_isSuccess, _error);

    public static bool operator ==(Result left, Result right) => left.Equals(right);
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the value if the operation was successful, otherwise the default value.
    /// </summary>
    public T? Value => _value;

    /// <summary>
    /// Gets the error if the operation failed, otherwise null.
    /// </summary>
    public Error? Error => _error;

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result<T> Failure(Error error) => new(false, default, error);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result<T> Failure(string errorMessage) => new(false, default, Error.Failure(errorMessage));

    /// <summary>
    /// Transforms the result value using the specified function if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> func)
    {
        return IsSuccess ? Result<TNew>.Success(func(Value)) : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Transforms the result using the specified function if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> func)
    {
        return IsSuccess ? func(Value) : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Executes the specified action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(Value);
        return this;
    }

    /// <summary>
    /// Executes the specified action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Converts this result to a non-generic result.
    /// </summary>
    public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(Error);

    public bool Equals(Result<T> other) => 
        _isSuccess == other._isSuccess && 
        EqualityComparer<T>.Default.Equals(_value, other._value) && 
        Equals(_error, other._error);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_isSuccess, _value, _error);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}