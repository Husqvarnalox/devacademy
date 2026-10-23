using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

decimal price = 120.50m;   // цена одной вещи
int quantity = 4;          // сколько штук куплено
decimal delivery = 150m;   // стоимость доставки
int a = 7;
int b = 4;

decimal total = price * quantity;
decimal withDelivery = total + delivery;
double average = (double)(a + b) / 2;
double share = (double)a / b;

Console.Write("Итого: ");
Console.WriteLine(total);
Console.Write("С доставкой: ");
Console.WriteLine(withDelivery);
Console.Write("Среднее: ");
Console.WriteLine(average);
Console.Write("Доля: ");
Console.WriteLine(share);
