string shop = Console.ReadLine() ?? "";
string item = Console.ReadLine() ?? "";
int quantity = int.Parse(Console.ReadLine() ?? "");
int price = int.Parse(Console.ReadLine() ?? "");

PrintHeader(shop);
PrintItem(item, quantity, price);
PrintItem("Пакет", 1, 5);

static void PrintHeader(string shop)
{
    Console.WriteLine($"=== {shop} ===");
}

static void PrintItem(string item, int quantity, int price)
{
    int total = quantity * price;
    Console.WriteLine($"{item} x {quantity} = {total}");
}
