namespace DevFlow.SharedKernel.Extensions;

/// <summary>
/// Extension methods for collections.
/// </summary>
public static class CollectionExtensions
{
  /// <summary>
  /// Checks if a collection is null or empty.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection to check</param>
  /// <returns>True if the collection is null or empty</returns>
  public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
  {
    return collection is null || !collection.Any();
  }

  /// <summary>
  /// Safely gets an element at the specified index.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="index">The index</param>
  /// <returns>The element at the index, or default if index is out of range</returns>
  public static T? SafeElementAt<T>(this IEnumerable<T> collection, int index)
  {
    return collection.Skip(index).FirstOrDefault();
  }

  /// <summary>
  /// Executes an action for each element in the collection.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="action">The action to execute</param>
  public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
  {
    foreach (var item in collection)
    {
      action(item);
    }
  }

  /// <summary>
  /// Executes an action for each element in the collection with the index.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="action">The action to execute</param>
  public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
  {
    var index = 0;
    foreach (var item in collection)
    {
      action(item, index++);
    }
  }

  /// <summary>
  /// Splits a collection into chunks of the specified size.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection to chunk</param>
  /// <param name="chunkSize">The size of each chunk</param>
  /// <returns>An enumerable of chunks</returns>
  public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> collection, int chunkSize)
  {
    if (chunkSize <= 0)
      throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));

    var list = collection.ToList();
    for (var i = 0; i < list.Count; i += chunkSize)
    {
      yield return list.Skip(i).Take(chunkSize);
    }
  }

  /// <summary>
  /// Adds an item to a collection if it doesn't already exist.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="item">The item to add</param>
  /// <returns>True if the item was added, false if it already existed</returns>
  public static bool AddIfNotExists<T>(this ICollection<T> collection, T item)
  {
    if (collection.Contains(item))
      return false;

    collection.Add(item);
    return true;
  }

  /// <summary>
  /// Removes all items from a collection that match the predicate.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="predicate">The predicate to match</param>
  /// <returns>The number of items removed</returns>
  public static int RemoveAll<T>(this ICollection<T> collection, Func<T, bool> predicate)
  {
    var itemsToRemove = collection.Where(predicate).ToList();
    foreach (var item in itemsToRemove)
    {
      collection.Remove(item);
    }
    return itemsToRemove.Count;
  }

  /// <summary>
  /// Converts an enumerable to a HashSet.
  /// </summary>
  /// <typeparam name="T">The type of elements in the collection</typeparam>
  /// <param name="collection">The collection</param>
  /// <param name="comparer">The equality comparer</param>
  /// <returns>A HashSet containing the elements</returns>
  public static HashSet<T> ToHashSet<T>(this IEnumerable<T> collection, IEqualityComparer<T>? comparer = null)
  {
    return new HashSet<T>(collection, comparer);
  }
}