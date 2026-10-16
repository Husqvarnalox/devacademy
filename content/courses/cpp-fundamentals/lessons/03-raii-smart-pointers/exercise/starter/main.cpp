#include <iostream>
#include <map>
#include <memory>
#include <string>

struct Resource {
    std::string name;
    explicit Resource(std::string n) : name(std::move(n)) { std::cout << "created " << name << "\n"; }
    ~Resource() { std::cout << "destroyed " << name << "\n"; }
};

using Owner = std::shared_ptr<Resource>;

int main() {
    int n;
    std::cin >> n;
    std::map<std::string, Owner> owners;                  // основные владельцы
    std::map<std::string, Owner> holders;                 // дополнительные владельцы (share)
    std::map<std::string, Owner> watchers;  // TODO: наблюдатель не должен владеть ресурсом

    for (int i = 0; i < n; ++i) {
        std::string cmd, a, b;
        std::cin >> cmd >> a;
        if (cmd == "share") std::cin >> b;

        if (cmd == "make") {
            if (owners.count(a)) {
                std::cout << "error: " << a << " exists\n";
            } else {
                owners[a] = std::make_shared<Resource>(a);
            }
        } else if (cmd == "watch") {
            auto it = owners.find(a);
            if (it == owners.end()) {
                std::cout << "error: no " << a << "\n";
            } else {
                watchers[a] = it->second;
            }
        } else if (cmd == "share") {
            auto it = owners.find(a);
            if (it == owners.end()) {
                std::cout << "error: no " << a << "\n";
            } else {
                holders[b] = it->second;
            }
        } else if (cmd == "drop") {
            owners.erase(a);
        } else if (cmd == "release") {
            holders.erase(a);
        } else if (cmd == "status") {
            auto it = watchers.find(a);
            if (it == watchers.end()) {
                std::cout << "error: no watcher " << a << "\n";
            } else if (it->second.use_count() == 0) {
                std::cout << a << " expired\n";
            } else {
                std::cout << a << " alive owners=" << it->second.use_count() << "\n";
            }
        }
    }
    std::cout << "shutdown\n";
    owners.clear();
    holders.clear();
}
