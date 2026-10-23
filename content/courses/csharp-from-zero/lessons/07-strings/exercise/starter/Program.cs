using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string shop = "Книжный";
string product = "Тетрадь";
decimal price = 45.5m;
int quantity = 3;
decimal total = price * quantity;

Console.WriteLine($"Магазин: {shop}");
// TODO: напечатайте строку "Товар: Тетрадь x 3" с помощью $"..." (название и количество берите из переменных)
// TODO: напечатайте строку "Цена: 45.50" - цена с двумя знаками после точки
// TODO: напечатайте строку "Итого: 136.50"
// TODO: напечатайте строку "В названии символов: 7" - длину возьмите из product.Length
