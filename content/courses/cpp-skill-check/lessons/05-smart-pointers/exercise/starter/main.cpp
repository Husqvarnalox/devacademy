#include <iostream>
#include <memory>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

// Сколько объектов Item сейчас живо.
int g_alive = 0;

struct Item {
    std::string name;
    int qty;
    Item(std::string n, int q) : name(std::move(n)), qty(q) {
        ++g_alive;
    }
    Item(const Item&) = delete;
    Item& operator=(const Item&) = delete;
    ~Item() {
        --g_alive;
    }
};

// Фабрика: создаёт Item в куче и отдаёт единоличное владение вызывающему.
std::unique_ptr<Item> makeItem(const std::string& name, int qty) {
    // TODO: создайте Item через std::make_unique и верните его
    (void)name;
    (void)qty;
    return std::make_unique<Item>("", 0);
}

// Принимает владение (sink): возвращает qty, а Item уничтожается при выходе из функции.
int consume(std::unique_ptr<Item> item) {
    // TODO: верните qty; параметр уже владеет объектом, и тот уничтожится при выходе из функции
    (void)item;
    return 0;
}

// Добавляет в holders ещё одного владельца того же объекта.
void addHolder(std::vector<std::shared_ptr<Item>>& holders, const std::shared_ptr<Item>& item) {
    // TODO: добавьте копию умного указателя в holders
    (void)holders;
    (void)item;
}

// Возвращает qty объекта, если он ещё жив, иначе -1. Не продлевает жизнь объекта надолго.
int qtyOrMinusOne(const std::weak_ptr<Item>& weak) {
    // TODO: получите shared_ptr через lock() и верните qty, а если объекта уже нет, верните -1
    (void)weak;
    return -1;
}

// ----- Дальше основная часть программы: её менять не нужно -----

int main() {
    std::string line;
    while (std::getline(std::cin, line)) {
        std::istringstream in(line);
        std::string command;
        in >> command;
        if (command == "unique") {
            std::string name;
            int qty = 0;
            in >> name >> qty;
            std::unique_ptr<Item> p = makeItem(name, qty);
            std::cout << "alive=" << g_alive << " name=" << p->name << "\n";
            int got = consume(std::move(p));
            std::cout << "got=" << got << " alive=" << g_alive << " empty=" << (p == nullptr ? "yes" : "no") << "\n";
        } else if (command == "shared") {
            std::string name;
            int qty = 0;
            int extra = 0;
            in >> name >> qty >> extra;
            std::shared_ptr<Item> owner = std::make_shared<Item>(name, qty);
            std::weak_ptr<Item> watcher = owner;
            std::vector<std::shared_ptr<Item>> holders;
            for (int i = 0; i < extra; ++i) {
                addHolder(holders, owner);
            }
            std::cout << "owners=" << owner.use_count() << " qty=" << qtyOrMinusOne(watcher) << "\n";
            holders.clear();
            std::cout << "owners=" << owner.use_count() << " alive=" << g_alive << "\n";
            owner.reset();
            std::cout << "qty=" << qtyOrMinusOne(watcher) << " alive=" << g_alive << "\n";
        }
    }
}
