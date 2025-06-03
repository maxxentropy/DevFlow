#nullable enable

using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;

namespace DevFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Helper class for creating JSON-based value comparers for EF Core.
/// </summary>
public static class JsonValueComparerHelper
{
    // Fixed JSON serializer options to use consistently
    public static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a value comparer for JSON-serialized collections.
    /// </summary>
    public static ValueComparer<T> CreateJsonValueComparer<T>() where T : class
    {
        // ValueComparer constructor:
        // 1. Func<T, T, bool> equals - compares two instances for equality
        // 2. Func<T, int> hashCode - generates hash code from instance
        // 3. Func<T, T> snapshotFactory - creates snapshot of instance
        // Create proper Expression trees for each parameter
        
        // Equality comparison expression - use nullable parameter types to match ValueComparer expectations
        Expression<Func<T?, T?, bool>> equalsExpression = (c1, c2) => JsonCompareEquality(c1, c2);
        
        // Hash code generation expression
        Expression<Func<T, int>> hashCodeExpression = c => JsonGenerateHashCode(c);
        
        // Snapshot factory expression
        Expression<Func<T, T>> snapshotExpression = c => JsonSnapshotFactory(c);
        
        return new ValueComparer<T>(
            equalsExpression,
            hashCodeExpression,
            snapshotExpression
        );
    }

    // Separate snapshot factory method to avoid the null warning in the lambda
    private static T JsonSnapshotFactory<T>(T c) where T : class
    {
        if (c == null)
        {
            // ValueComparer accepts null returns for snapshot factory
            // This cast is safe because the ValueComparer constructor accepts null 
            // from the snapshot factory, and we've verified c is null
            return default!;
        }
        
        return JsonCreateSnapshot(c) ?? default!;
    }

    // Helper methods separated to avoid expression tree limitations
    
    // Fixed nullable reference type issue by using static methods and fixing the call sites
    private static bool JsonCompareEquality<T>(T? left, T? right) where T : class
    {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;
                
        var json1 = JsonSerializer.Serialize(left, _jsonOptions);
        var json2 = JsonSerializer.Serialize(right, _jsonOptions);
        return json1 == json2;
    }

    private static int JsonGenerateHashCode<T>(T? value) where T : class
    {
        if (value == null)
            return 0;
                
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        return json.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    [return: MaybeNull]
    private static T JsonCreateSnapshot<T>(T? value) where T : class
    {
        if (value == null)
            return null;
                
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        
        // Use pattern matching for more type-safety
        if (result == null && value is Dictionary<string, object>)
        {
            return (T)(object)new Dictionary<string, object>();
        }
        
        return result;
    }
}

