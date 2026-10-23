int price = 85;       // цена одной тетради, руб.
int quantity = 4;     // сколько тетрадей купили
int paid = 500;       // сколько дал покупатель
int notebooks = 30;   // сколько тетрадей нужно разложить по пачкам
int packSize = 12;    // в одной пачке 12 тетрадей

// TODO: объявите total - стоимость покупки (цена умножить на количество)
// TODO: объявите change - сдачу (сколько дал покупатель минус стоимость)
// TODO: объявите packs - сколько полных пачек получится из notebooks
// TODO: объявите extra - сколько тетрадей останется вне пачек

Console.Write("Сумма: ");
Console.WriteLine(total);
Console.Write("Сдача: ");
Console.WriteLine(change);
Console.Write("Полных пачек: ");
Console.WriteLine(packs);
Console.Write("Отдельных тетрадей: ");
Console.WriteLine(extra);
