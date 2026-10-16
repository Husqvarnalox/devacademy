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

    // Возвращает ключ вытесненной записи, если она была.
    std::optional<std::string> put(const std::string& key, long long value) {
        auto it = index_.find(key);
        if (it != index_.end()) {
            it->second->second = value;
            order_.splice(order_.begin(), order_, it->second);
            return std::nullopt;
        }
        std::optional<std::string> evicted;
        if (capacity_ > 0 && order_.size() == capacity_) {
            evicted = order_.back().first;
            index_.erase(order_.back().first);
            order_.pop_back();
        }
        if (capacity_ == 0) return key;  // кэш нулевой ёмкости ничего не хранит
        order_.emplace_front(key, value);
        index_[key] = order_.begin();
        return evicted;
    }

    std::optional<long long> get(const std::string& key) {
        auto it = index_.find(key);
        if (it == index_.end()) return std::nullopt;
        order_.splice(order_.begin(), order_, it->second);
        return it->second->second;
    }

    bool erase(const std::string& key) {
        auto it = index_.find(key);
        if (it == index_.end()) return false;
        order_.erase(it->second);
        index_.erase(it);
        return true;
    }

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
