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

// Прямоугольник: ширина и высота задаются в конструкторе.
class Rect : public Shape {
public:
    Rect(int w, int h) {
        // TODO: инициализируйте w_ и h_ в списке инициализации: Rect(int w, int h) : w_(w), h_(h) {}
        (void)w;
        (void)h;
    }
    int width() const { return w_; }
    int height() const { return h_; }
    int area() const override {
        // TODO: верните площадь прямоугольника
        return 0;
    }
    std::string name() const override {
        // TODO: верните "rect"
        return "?";
    }

private:
    int w_ = 0;
    int h_ = 0;
};

// Квадрат - частный случай прямоугольника.
class Square : public Rect {
public:
    explicit Square(int side) : Rect(0, 0) {
        // TODO: квадрат - прямоугольник со сторонами side и side; передайте их в конструктор Rect
        (void)side;
    }
    // TODO: переопределите name() так, чтобы оно возвращало "square"
};

// Прямоугольники равны, если равны ширина и высота.
bool operator==(const Rect& a, const Rect& b) {
    // TODO: сравните ширину и высоту
    (void)a;
    (void)b;
    return false;
}

// Печать фигуры в виде name(area); должна работать для любой фигуры через ссылку на Shape.
std::ostream& operator<<(std::ostream& out, const Shape& shape) {
    // TODO: выведите name(area), например rect(6)
    (void)shape;
    return out;
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
