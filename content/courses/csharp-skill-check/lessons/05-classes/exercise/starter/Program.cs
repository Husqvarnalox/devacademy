using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: она читает фигуры (по одной на строку) и печатает площади.
// Допишите классы и два метода внизу файла. Основную часть и интерфейс IShape не меняйте.
List<IShape> shapes = new List<IShape>();
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "rect":
                shapes.Add(new Rectangle(double.Parse(parts[1]), double.Parse(parts[2])));
                break;
            case "square":
                shapes.Add(new Square(double.Parse(parts[1])));
                break;
            case "circle":
                shapes.Add(new Circle(double.Parse(parts[1])));
                break;
        }
    }
    raw = Console.ReadLine();
}

foreach (IShape shape in shapes)
{
    Console.WriteLine($"{shape.Name}: {shape.Area():F2}");
}
Console.WriteLine($"всего: {TotalArea(shapes):F2}");
Console.WriteLine($"крупнейшая: {Largest(shapes)}");

static double TotalArea(List<IShape> shapes)
{
    // TODO: сумма площадей всех фигур (для пустого списка - 0)
    return -1;
}

static string Largest(List<IShape> shapes)
{
    // TODO: название фигуры с наибольшей площадью; при равенстве - первой из списка; для пустого списка - "нет"
    return "?";
}

interface IShape
{
    string Name { get; }
    double Area();
}

class Rectangle : IShape
{
    // TODO: конструктор (double width, double height), название "прямоугольник", площадь width * height.
    //       Свойство названия должно быть virtual, чтобы Square могла его переопределить.
    public Rectangle(double width, double height)
    {
    }

    public virtual string Name => "?";

    public double Area()
    {
        return 0;
    }
}

class Square : Rectangle
{
    // TODO: конструктор (double side), который вызывает базовый конструктор; название "квадрат"
    public Square(double side) : base(0, 0)
    {
    }
}

class Circle : IShape
{
    // TODO: конструктор (double radius), название "круг", площадь Math.PI * radius * radius
    public Circle(double radius)
    {
    }

    public string Name => "?";

    public double Area()
    {
        return 0;
    }
}
