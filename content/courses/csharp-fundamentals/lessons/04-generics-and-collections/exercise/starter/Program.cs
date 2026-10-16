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

// TODO: этот кэш никого не вытесняет и не помнит порядок использования.
// Реализуйте LRU: при Put в полном кэше удаляйте наименее недавно использованный ключ и возвращайте его;
// Put существующего ключа и успешный TryGet делают ключ самым свежим; KeysByRecency перечисляет от свежего к старому.
// Все операции должны быть O(1): подойдёт Dictionary + LinkedList.
sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey, TValue> map = new();

    public LruCache(int capacity) => this.capacity = capacity;

    public int Count => map.Count;

    public TKey? Put(TKey key, TValue value)
    {
        map[key] = value;
        return default;
    }

    public bool TryGet(TKey key, out TValue value) => map.TryGetValue(key, out value!);

    public IEnumerable<TKey> KeysByRecency() => map.Keys;
}
