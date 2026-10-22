int age = int.Parse(Console.ReadLine() ?? "");

if (age < 7)
{
    Console.WriteLine("Бесплатно");
}
else if (age < 18)
{
    Console.WriteLine("Детский билет: 100");
}
else if (age < 65)
{
    Console.WriteLine("Взрослый билет: 300");
}
else
{
    Console.WriteLine("Льготный билет: 150");
}
