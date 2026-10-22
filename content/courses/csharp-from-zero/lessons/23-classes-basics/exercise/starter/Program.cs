int n = int.Parse(Console.ReadLine() ?? "");
List<Item> items = new List<Item>();
for (int i = 0; i < n; i++)
{
    items.Add(ReadItem(Console.ReadLine() ?? ""));
}

int total = 0;
foreach (Item item in items)
{
    total += item.Cost();
}
Console.WriteLine($"Позиций: {items.Count}");
Console.WriteLine($"Итого: {total}");

if (items.Count > 0)
{
    Item best = items[0];
    foreach (Item item in items)
    {
        if (item.Cost() > best.Cost())
        {
            best = item;
        }
    }
    Console.WriteLine($"Самая дорогая: {best.Name} ({best.Cost()})");
}

static Item ReadItem(string line)
{
    // TODO: разберите строку "название цена количество" и верните заполненный объект Item
    return new Item();
}

class Item
{
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public int Quantity { get; set; }

    public int Cost()
    {
        // TODO: верните стоимость позиции (цена умножить на количество)
        return 0;
    }
}
