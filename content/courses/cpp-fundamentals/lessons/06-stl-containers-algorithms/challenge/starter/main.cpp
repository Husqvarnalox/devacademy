#include <iostream>
#include <list>
#include <optional>
#include <sstream>
#include <string>
#include <unordered_map>
#include <utility>

// LRU-кэш: список хранит записи от самой свежей к самой старой, а хеш-таблица ищет узел списка по ключу.
// Итераторы std::list не инвалидируются при splice, поэтому они годятся как «закладки» в таблице.
class LruCache {
public:
    explicit LruCache(std::size_t capacity) : capacity_(capacity) {}

    // TODO: put - вставить или обновить запись и сделать её самой свежей; если ёмкость превышена,
    // вытеснить самую старую запись и вернуть её ключ. При capacity = 0 запись не сохраняется,
    // а в ответ возвращается сам вставляемый ключ.
    std::optional<std::string> put(const std::string& key, long long value) {
        (void)key;
        (void)value;
        return std::nullopt;
    }

    // TODO: get - вернуть значение и сделать запись самой свежей; если ключа нет - std::nullopt.
    std::optional<long long> get(const std::string& key) {
        (void)key;
        return std::nullopt;
    }

    // TODO: erase - удалить запись; true, если она была.
    bool erase(const std::string& key) {
        (void)key;
        return false;
    }

    std::size_t capacity() const { return capacity_; }

    std::size_t size() const { return order_.size(); }

    std::string dump() const {
        if (order_.empty()) return "(empty)";
        std::string out;
        for (const auto& [key, value] : order_) {
            if (!out.empty()) out += ' ';
            out += key;
        }
        return out;
    }

private:
    std::size_t capacity_;
    std::list<std::pair<std::string, long long>> order_;
    std::unordered_map<std::string, std::list<std::pair<std::string, long long>>::iterator> index_;
};

int main() {
    std::size_t capacity;
    int n;
    std::cin >> capacity >> n;
    LruCache cache(capacity);
    for (int i = 0; i < n; ++i) {
        std::string cmd;
        std::cin >> cmd;
        if (cmd == "put") {
            std::string key;
            long long value;
            std::cin >> key >> value;
            if (auto evicted = cache.put(key, value)) std::cout << "evicted " << *evicted << "\n";
        } else if (cmd == "get") {
            std::string key;
            std::cin >> key;
            if (auto v = cache.get(key)) std::cout << *v << "\n";
            else std::cout << "miss\n";
        } else if (cmd == "del") {
            std::string key;
            std::cin >> key;
            std::cout << (cache.erase(key) ? "deleted" : "absent") << "\n";
        } else if (cmd == "size") {
            std::cout << "size=" << cache.size() << "\n";
        } else if (cmd == "dump") {
            std::cout << cache.dump() << "\n";
        }
    }
}
