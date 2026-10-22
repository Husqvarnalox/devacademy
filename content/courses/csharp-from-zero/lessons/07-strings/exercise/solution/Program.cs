using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string shop = "Книжный";
string product = "Тетрадь";
decimal price = 45.5m;
int quantity = 3;
decimal total = price * quantity;

Console.WriteLine($"Магазин: {shop}");
Console.WriteLine($"Товар: {product} x {quantity}");
Console.WriteLine($"Цена: {price:F2}");
Console.WriteLine($"Итого: {total:F2}");
Console.WriteLine($"В названии символов: {product.Length}");
