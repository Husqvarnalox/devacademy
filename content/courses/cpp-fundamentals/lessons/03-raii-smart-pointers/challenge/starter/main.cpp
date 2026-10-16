#include <algorithm>
#include <iostream>
#include <memory>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

int g_live = 0;       // сколько узлов существует сейчас
int g_destroyed = 0;  // сколько узлов уничтожено за всё время

struct Node {
    std::string name;
    Node* parent;  // не владеет: родитель живёт дольше ребёнка
    std::vector<std::unique_ptr<Node>> children;  // владеет поддеревьями

    Node(std::string n, Node* p) : name(std::move(n)), parent(p) { ++g_live; }
    Node(const Node&) = delete;
    Node& operator=(const Node&) = delete;
    ~Node() {
        --g_live;
        ++g_destroyed;
    }
};

class Tree {
public:
    Tree() : root_(std::make_unique<Node>("root", nullptr)) { index_["root"] = root_.get(); }

    // TODO: реализуйте add, remove, move и path (см. условие). Сейчас все они возвращают заглушку.
    std::string add(const std::string& parent, const std::string& child) {
        (void)parent;
        (void)child;
        return "error: not implemented";
    }

    std::string remove(const std::string& name) {
        (void)name;
        return "error: not implemented";
    }

    std::string move(const std::string& name, const std::string& newParent) {
        (void)name;
        (void)newParent;
        return "error: not implemented";
    }

    std::string path(const std::string& name) const {
        (void)name;
        return "error: not implemented";
    }

    void print(std::ostream& out) const { printNode(out, root_.get(), 0); }

private:
    Node* find(const std::string& name) const {
        auto it = index_.find(name);
        return it == index_.end() ? nullptr : it->second;
    }

    void unindex(Node* n) {
        index_.erase(n->name);
        for (const std::unique_ptr<Node>& c : n->children) unindex(c.get());
    }

    static void printNode(std::ostream& out, const Node* n, int depth) {
        out << std::string(depth * 2, ' ') << n->name << "\n";
        for (const std::unique_ptr<Node>& c : n->children) printNode(out, c.get(), depth + 1);
    }

    std::unique_ptr<Node> root_;
    std::unordered_map<std::string, Node*> index_;  // несущий индекс: указатели не владеют
};

int main() {
    int n;
    std::cin >> n;
    std::string line;
    std::getline(std::cin, line);
    Tree tree;
    for (int i = 0; i < n; ++i) {
        std::getline(std::cin, line);
        std::istringstream in(line);
        std::string cmd, a, b;
        in >> cmd >> a >> b;
        if (cmd == "add") std::cout << tree.add(a, b) << "\n";
        else if (cmd == "remove") std::cout << tree.remove(a) << "\n";
        else if (cmd == "move") std::cout << tree.move(a, b) << "\n";
        else if (cmd == "path") std::cout << tree.path(a) << "\n";
        else if (cmd == "print") tree.print(std::cout);
        else if (cmd == "count") std::cout << "nodes=" << g_live << "\n";
        else std::cout << "error: unknown command\n";
    }
}
