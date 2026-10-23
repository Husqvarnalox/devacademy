int price = 85;       // цена одной тетради, руб.
int quantity = 4;     // сколько тетрадей купили
int paid = 500;       // сколько дал покупатель
int notebooks = 30;   // сколько тетрадей нужно разложить по пачкам
int packSize = 12;    // в одной пачке 12 тетрадей

int total = price * quantity;
int change = paid - total;
int packs = notebooks / packSize;
int extra = notebooks % packSize;

Console.Write("Сумма: ");
Console.WriteLine(total);
Console.Write("Сдача: ");
Console.WriteLine(change);
Console.Write("Полных пачек: ");
Console.WriteLine(packs);
Console.Write("Отдельных тетрадей: ");
Console.WriteLine(extra);
