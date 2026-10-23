// Основная часть программы готова: она читает команды (по одной на строку) и печатает результат.
// Допишите внизу файла MakeOp, SumSquaresAsync и класс Counter. Основную часть не меняйте.
List<Func<int, int>> pipeline = new List<Func<int, int>>();
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "op":
                Func<int, int>? op = MakeOp(parts[1], int.Parse(parts[2]));
                if (op == null)
                {
                    Console.WriteLine($"неизвестная операция: {parts[1]}");
                }
                else
                {
                    pipeline.Add(op);
                }
                break;
            case "reset":
                pipeline.Clear();
                break;
            case "run":
                int current = int.Parse(parts[1]);
                foreach (Func<int, int> step in pipeline)
                {
                    current = step(current);
                }
                Console.WriteLine(current);
                break;
            case "count":
                Counter counter = new Counter();
                counter.Changed += value => Console.WriteLine($"значение: {value}");
                for (int i = 1; i < parts.Length; i++)
                {
                    counter.Add(int.Parse(parts[i]));
                }
                Console.WriteLine($"итого: {counter.Value}");
                break;
            case "squares":
                List<int> numbers = parts.Skip(1).Select(p => int.Parse(p)).ToList();
                int total = await SumSquaresAsync(numbers);
                Console.WriteLine(total);
                break;
        }
    }
    raw = Console.ReadLine();
}

static Func<int, int>? MakeOp(string name, int arg)
{
    // TODO: "add" - x + arg, "sub" - x - arg, "mul" - x * arg; для другого имени - null
    return null;
}

static Task<int> SumSquaresAsync(List<int> values)
{
    // TODO: превратить в async-метод: посчитать квадраты через Task.Run, дождаться всех (Task.WhenAll)
    //       и вернуть их сумму; для пустого списка - 0
    return Task.FromResult(-1);
}

class Counter
{
    public int Value { get; private set; }

    public event Action<int>? Changed;

    public void Add(int n)
    {
        // TODO: при n == 0 ничего не делать; иначе изменить Value и вызвать Changed с новым значением
        //       (если подписчиков нет, событие вызывать нельзя - будет NullReferenceException)
    }
}
