using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

decimal price = 120.50m;   // цена одной вещи
int quantity = 4;          // сколько штук куплено
decimal delivery = 150m;   // стоимость доставки
int a = 7;
int b = 4;

// TODO: объявите total (decimal) - цена умножить на количество
// TODO: объявите withDelivery (decimal) - total плюс доставка
// TODO: объявите average (double) - среднее a и b, с дробной частью
// TODO: объявите share (double) - результат деления a на b, с дробной частью

Console.Write("Итого: ");
Console.WriteLine(total);
Console.Write("С доставкой: ");
Console.WriteLine(withDelivery);
Console.Write("Среднее: ");
Console.WriteLine(average);
Console.Write("Доля: ");
Console.WriteLine(share);
