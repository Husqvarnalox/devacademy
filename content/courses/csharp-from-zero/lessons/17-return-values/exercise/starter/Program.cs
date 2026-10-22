int price = int.Parse(Console.ReadLine() ?? "");
int percent = int.Parse(Console.ReadLine() ?? "");
int age = int.Parse(Console.ReadLine() ?? "");

Console.WriteLine($"Цена со скидкой: {FinalPrice(price, percent)}");
if (IsAdult(age))
{
    Console.WriteLine("Совершеннолетний: да");
}
else
{
    Console.WriteLine("Совершеннолетний: нет");
}

static int FinalPrice(int price, int percent)
{
    // TODO: верните цену после скидки: price - price * percent / 100
    return 0;
}

static bool IsAdult(int age)
{
    // TODO: верните true, если age не меньше 18, иначе false
    return false;
}
