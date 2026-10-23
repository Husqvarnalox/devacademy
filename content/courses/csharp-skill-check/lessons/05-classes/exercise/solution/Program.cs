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
    double total = 0;
    foreach (IShape shape in shapes)
    {
        total += shape.Area();
    }
    return total;
}

static string Largest(List<IShape> shapes)
{
    if (shapes.Count == 0)
    {
        return "нет";
    }
    IShape best = shapes[0];
    foreach (IShape shape in shapes)
    {
        if (shape.Area() > best.Area())
        {
            best = shape;
        }
    }
    return best.Name;
}

interface IShape
{
    string Name { get; }
    double Area();
}

class Rectangle : IShape
{
    private readonly double _width;
    private readonly double _height;

    public Rectangle(double width, double height)
    {
        _width = width;
        _height = height;
    }

    public virtual string Name => "прямоугольник";

    public double Area()
    {
        return _width * _height;
    }
}

class Square : Rectangle
{
    public Square(double side) : base(side, side)
    {
    }

    public override string Name => "квадрат";
}

class Circle : IShape
{
    private readonly double _radius;

    public Circle(double radius)
    {
        _radius = radius;
    }

    public string Name => "круг";

    public double Area()
    {
        return Math.PI * _radius * _radius;
    }
}
