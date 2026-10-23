#include <iostream>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

// Абстрактная фигура.
class Shape {
public:
    virtual ~Shape() = default;
    virtual int area() const = 0;
    virtual std::string name() const = 0;
};

// Прямоугольник: ширина и высота задаются в конструкторе через список инициализации.
class Rect : public Shape {
public:
    Rect(int w, int h) : w_(w), h_(h) {}
    int width() const { return w_; }
    int height() const { return h_; }
    int area() const override { return w_ * h_; }
    std::string name() const override { return "rect"; }

private:
    int w_;
    int h_;
};

// Квадрат - частный случай прямоугольника.
class Square : public Rect {
public:
    explicit Square(int side) : Rect(side, side) {}
    std::string name() const override { return "square"; }
};

// Прямоугольники равны, если равны ширина и высота.
bool operator==(const Rect& a, const Rect& b) {
    return a.width() == b.width() && a.height() == b.height();
}

// Печать фигуры в виде name(area); должна работать для любой фигуры через ссылку на Shape.
std::ostream& operator<<(std::ostream& out, const Shape& shape) {
    return out << shape.name() << "(" << shape.area() << ")";
}

// ----- Дальше основная часть программы: её менять не нужно -----

std::string nameByValue(Rect r) {
    return r.name();
}

std::string nameByReference(const Rect& r) {
    return r.name();
}

int main() {
    std::string line;
    while (std::getline(std::cin, line)) {
        std::istringstream in(line);
        std::string command;
        in >> command;
        if (command == "rect") {
            int w = 0;
            int h = 0;
            in >> w >> h;
            Rect r(w, h);
            std::cout << r << "\n";
        } else if (command == "square") {
            int s = 0;
            in >> s;
            Square sq(s);
            std::cout << sq << "\n";
        } else if (command == "same") {
            int w1 = 0;
            int h1 = 0;
            int w2 = 0;
            int h2 = 0;
            in >> w1 >> h1 >> w2 >> h2;
            Rect a(w1, h1);
            Rect b(w2, h2);
            std::cout << (a == b ? "equal" : "different") << "\n";
        } else if (command == "slice") {
            int s = 0;
            in >> s;
            Square sq(s);
            std::cout << nameByReference(sq) << " " << nameByValue(sq) << "\n";
        } else if (command == "total") {
            std::vector<std::unique_ptr<Shape>> shapes;
            std::string kind;
            while (in >> kind) {
                if (kind == "r") {
                    int w = 0;
                    int h = 0;
                    in >> w >> h;
                    shapes.push_back(std::make_unique<Rect>(w, h));
                } else {
                    int s = 0;
                    in >> s;
                    shapes.push_back(std::make_unique<Square>(s));
                }
            }
            int sum = 0;
            for (const auto& shape : shapes) {
                std::cout << *shape << " ";
                sum += shape->area();
            }
            std::cout << "total=" << sum << "\n";
        }
    }
}
