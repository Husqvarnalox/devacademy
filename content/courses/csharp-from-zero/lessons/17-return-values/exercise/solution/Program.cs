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
    int discount = price * percent / 100;
    return price - discount;
}

static bool IsAdult(int age)
{
    return age >= 18;
}
