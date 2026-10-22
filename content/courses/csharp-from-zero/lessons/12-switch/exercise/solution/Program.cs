int month = int.Parse(Console.ReadLine() ?? "");

string season = month switch
{
    12 or 1 or 2 => "Зима",
    3 or 4 or 5 => "Весна",
    6 or 7 or 8 => "Лето",
    9 or 10 or 11 => "Осень",
    _ => "Такого месяца нет"
};

Console.WriteLine(season);
