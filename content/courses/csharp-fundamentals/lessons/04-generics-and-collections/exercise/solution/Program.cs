#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int capacity = int.Parse(lines[0], CultureInfo.InvariantCulture);
int n = int.Parse(lines[1], CultureInfo.InvariantCulture);

var cache = new LruCache<string, int>(capacity);

for (int i = 0; i < n; i++)
{
    string[] a = lines[2 + i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    switch (a[0])
    {
        case "put":
            if (cache.Put(a[1], int.Parse(a[2], CultureInfo.InvariantCulture)) is { } evicted)
                Console.WriteLine($"evict {evicted}");
            break;
        case "get":
            Console.WriteLine(cache.TryGet(a[1], out int value) ? value.ToString(CultureInfo.InvariantCulture) : "miss");
            break;
        case "keys":
            Console.WriteLine(cache.Count == 0 ? "empty" : string.Join(' ', cache.KeysByRecency()));
            break;
        case "size":
            Console.WriteLine($"size={cache.Count}");
            break;
    }
}

/// <summary>Кэш с вытеснением наименее недавно использованного ключа. Все операции - O(1).</summary>
sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> map = new();
    private readonly LinkedList<(TKey Key, TValue Value)> order = new();   // первый узел - самый свежий

    public LruCache(int capacity) => this.capacity = capacity;

    public int Count => map.Count;

    /// <summary>Добавляет или обновляет пару. Возвращает вытесненный ключ или default.</summary>
    public TKey? Put(TKey key, TValue value)
    {
        if (map.TryGetValue(key, out var node))
        {
            node.Value = (key, value);
            order.Remove(node);
            order.AddFirst(node);
            return default;
        }

        TKey? evicted = default;
        if (map.Count == capacity)
        {
            var last = order.Last!;
            evicted = last.Value.Key;
            map.Remove(last.Value.Key);
            order.RemoveLast();
        }
        map[key] = order.AddFirst((key, value));
        return evicted;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (map.TryGetValue(key, out var node))
        {
            order.Remove(node);
            order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = default!;
        return false;
    }

    public IEnumerable<TKey> KeysByRecency()
    {
        foreach (var (key, _) in order) yield return key;
    }
}
