int n = int.Parse(Console.ReadLine() ?? "");

Console.WriteLine($"Чётное: {n % 2 == 0}");
Console.WriteLine($"Двузначное: {n >= 10 && n <= 99}");
Console.WriteLine($"Не равно нулю: {n != 0}");
Console.WriteLine($"Вне диапазона от 1 до 100: {n < 1 || n > 100}");
