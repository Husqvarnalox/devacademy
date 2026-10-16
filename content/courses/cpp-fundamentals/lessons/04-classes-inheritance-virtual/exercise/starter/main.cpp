#include <algorithm>
#include <iomanip>
#include <iostream>
#include <memory>
#include <numbers>
#include <string>
#include <vector>

class Shape {
public:
    virtual ~Shape() = default;
    // TODO: сделайте area и kind виртуальными (чисто виртуальными), чтобы вызов шёл по реальному типу
    double area() const { return 0; }
    std::string kind() const { return "shape"; }
};

class Circle : public Shape {
public:
    explicit Circle(int r) : r_(r) {}
    double area() const { return std::numbers::pi * r_ * r_; }
    std::string kind() const { return "circle"; }

private:
    int r_;
};

class Rect : public Shape {
public:
    Rect(int w, int h) : w_(w), h_(h) {}
    double area() const { return static_cast<double>(w_) * h_; }
    std::string kind() const { return "rect"; }

private:
    int w_, h_;
};

class Square final : public Rect {
public:
    explicit Square(int s) : Rect(s, s) {}
    std::string kind() const { return "square"; }
};

bool operator<(const Shape& a, const Shape& b) { return a.area() < b.area(); }

std::ostream& operator<<(std::ostream& out, const Shape& s) {
    return out << s.kind() << " area=" << std::fixed << std::setprecision(2) << s.area();
}

int main() {
    int n;
    std::cin >> n;
    std::vector<std::unique_ptr<Shape>> shapes;
    for (int i = 0; i < n; ++i) {
        std::string type;
        std::cin >> type;
        if (type == "circle") {
            int r;
            std::cin >> r;
            shapes.push_back(std::make_unique<Circle>(r));
        } else if (type == "rect") {
            int w, h;
            std::cin >> w >> h;
            shapes.push_back(w == h ? std::make_unique<Square>(w) : std::make_unique<Rect>(w, h));
        }
    }
    std::stable_sort(shapes.begin(), shapes.end(),
                     [](const std::unique_ptr<Shape>& a, const std::unique_ptr<Shape>& b) { return *a < *b; });
    double total = 0;
    for (const auto& s : shapes) {
        std::cout << *s << "\n";
        total += s->area();
    }
    std::cout << std::fixed << std::setprecision(2) << "total=" << total << "\n";
}
