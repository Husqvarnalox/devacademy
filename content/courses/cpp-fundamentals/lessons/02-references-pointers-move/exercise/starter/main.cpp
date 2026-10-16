#include <iostream>
#include <utility>
#include <vector>

struct Stats {
    int copies = 0;
    int moves = 0;
};
Stats stats;

class Packet {
public:
    explicit Packet(std::vector<long long> data) : data_(std::move(data)) {}

    Packet(const Packet& other) : data_(other.data_) { ++stats.copies; }

    Packet& operator=(const Packet& other) {
        if (this != &other) {
            data_ = other.data_;
            ++stats.copies;
        }
        return *this;
    }

    // TODO: добавьте конструктор перемещения и оператор присваивания перемещением.
    // Они должны забирать данные, оставлять источник пустым и считать stats.moves.

    std::size_t size() const { return data_.size(); }

    long long sum() const {
        long long s = 0;
        for (long long x : data_) s += x;
        return s;
    }

private:
    std::vector<long long> data_;
};

int main() {
    int n;
    std::cin >> n;
    std::vector<long long> values(n);
    for (long long& x : values) std::cin >> x;
    int m;
    std::cin >> m;

    Packet original(values);
    std::vector<Packet> inbox;
    inbox.reserve(m);
    for (int i = 0; i < m; ++i) inbox.push_back(Packet(values));  // временный объект: rvalue

    long long total = 0;
    for (const Packet& p : inbox) total += p.sum();

    Packet taken = std::move(original);
    Packet copy = taken;
    Packet target(std::vector<long long>{});
    target = std::move(copy);

    std::cout << "inbox=" << inbox.size() << " total=" << total << "\n";
    std::cout << "taken=" << taken.size() << " original=" << original.size() << " copy=" << copy.size()
              << " target=" << target.size() << "\n";
    std::cout << "copies=" << stats.copies << " moves=" << stats.moves << "\n";
}
