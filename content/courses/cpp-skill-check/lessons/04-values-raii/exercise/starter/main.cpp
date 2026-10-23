#include <iostream>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

// Увеличивает каждый элемент на 1 прямо в векторе вызывающего.
void incrementAll(std::vector<int>& v) {
    // TODO: увеличьте каждый элемент на 1 (параметр - ссылка, меняется вектор вызывающего)
    (void)v;
}

// Возвращает сумму элементов, не копируя вектор.
int sumOf(const std::vector<int>& v) {
    // TODO: верните сумму элементов
    (void)v;
    return 0;
}

// Получает копию вектора и возвращает копию с удвоенными элементами.
std::vector<int> doubled(std::vector<int> v) {
    // TODO: удвойте каждый элемент копии v и верните её
    return v;
}

// RAII-метка: при создании пишет в журнал "+имя", при уничтожении - "-имя".
class Tracer {
public:
    Tracer(std::vector<std::string>& log, std::string name) : log_(log), name_(std::move(name)) {
        // TODO: запишите в журнал "+имя"
    }
    // TODO: добавьте деструктор, который запишет в журнал "-имя"

    Tracer(const Tracer&) = delete;
    Tracer& operator=(const Tracer&) = delete;

private:
    std::vector<std::string>& log_;
    std::string name_;
};

// ----- Дальше основная часть программы: её менять не нужно -----

void printList(const std::vector<int>& v) {
    for (std::size_t i = 0; i < v.size(); ++i) {
        std::cout << (i == 0 ? "" : " ") << v[i];
    }
}

void nest(const std::vector<std::string>& names, std::size_t index, std::vector<std::string>& log) {
    if (index == names.size()) {
        log.push_back("body");
        return;
    }
    Tracer tracer(log, names[index]);
    nest(names, index + 1, log);
}

int main() {
    std::string line;
    while (std::getline(std::cin, line)) {
        std::istringstream in(line);
        std::string command;
        in >> command;
        if (command == "inc") {
            std::vector<int> v;
            for (int x; in >> x;) {
                v.push_back(x);
            }
            incrementAll(v);
            printList(v);
            std::cout << "\n";
        } else if (command == "sum") {
            std::vector<int> v;
            for (int x; in >> x;) {
                v.push_back(x);
            }
            std::cout << sumOf(v) << "\n";
        } else if (command == "double") {
            std::vector<int> original;
            for (int x; in >> x;) {
                original.push_back(x);
            }
            std::vector<int> result = doubled(original);
            printList(original);
            std::cout << " -> ";
            printList(result);
            std::cout << "\n";
        } else if (command == "scope") {
            std::vector<std::string> names;
            for (std::string name; in >> name;) {
                names.push_back(name);
            }
            std::vector<std::string> log;
            nest(names, 0, log);
            for (std::size_t i = 0; i < log.size(); ++i) {
                std::cout << (i == 0 ? "" : " ") << log[i];
            }
            std::cout << "\n";
        }
    }
}
