#include <iostream>
#include <string>
#include <vector>

// Функция-счётчик: статическая локальная переменная живёт всё время работы программы.
int nextId() {
    static int counter = 0;
    return ++counter;
}

int& closedCount() {
    static int closed = 0;
    return closed;
}

struct Scope {
    int id;
    std::string name;
    explicit Scope(std::string n) : id(nextId()), name(std::move(n)) {
        std::cout << "open " << id << " " << name << "\n";
    }
    ~Scope() {
        std::cout << "close " << id << " " << name << "\n";
        ++closedCount();
    }
};

// Каждое следующее слово открывается внутри области предыдущего.
void visit(const std::vector<std::string>& words, std::size_t i) {
    if (i == words.size()) return;
    Scope s(words[i]);
    visit(words, i + 1);
}

int main() {
    int n;
    std::cin >> n;
    std::vector<std::string> words(n);
    for (std::string& w : words) std::cin >> w;
    visit(words, 0);
    std::cout << "closed=" << closedCount() << "\n";
}
