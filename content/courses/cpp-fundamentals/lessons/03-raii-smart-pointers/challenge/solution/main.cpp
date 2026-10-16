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

    std::string add(const std::string& parent, const std::string& child) {
        Node* p = find(parent);
        if (!p) return "error: no " + parent;
        if (find(child)) return "error: duplicate " + child;
        p->children.push_back(std::make_unique<Node>(child, p));
        index_[child] = p->children.back().get();
        return "added " + child + " under " + parent;
    }

    std::string remove(const std::string& name) {
        Node* n = find(name);
        if (!n) return "error: no " + name;
        if (!n->parent) return "error: cannot remove root";
        unindex(n);  // имена нужно убрать из индекса до уничтожения узлов
        std::vector<std::unique_ptr<Node>>& siblings = n->parent->children;
        int before = g_destroyed;
        siblings.erase(std::remove_if(siblings.begin(), siblings.end(),
                                      [n](const std::unique_ptr<Node>& c) { return c.get() == n; }),
                       siblings.end());
        return "removed " + name + " (" + std::to_string(g_destroyed - before) + " nodes)";
    }

    std::string move(const std::string& name, const std::string& newParent) {
        Node* n = find(name);
        if (!n) return "error: no " + name;
        Node* p = find(newParent);
        if (!p) return "error: no " + newParent;
        if (!n->parent) return "error: cannot move root";
        for (Node* a = p; a; a = a->parent)
            if (a == n) return "error: cycle";
        std::vector<std::unique_ptr<Node>>& siblings = n->parent->children;
        auto it = std::find_if(siblings.begin(), siblings.end(),
                               [n](const std::unique_ptr<Node>& c) { return c.get() == n; });
        std::unique_ptr<Node> owned = std::move(*it);  // владение временно у нас
        siblings.erase(it);
        owned->parent = p;
        p->children.push_back(std::move(owned));
        return "moved " + name + " under " + newParent;
    }

    std::string path(const std::string& name) const {
        const Node* n = find(name);
        if (!n) return "error: no " + name;
        std::string result = n->name;
        for (const Node* a = n->parent; a; a = a->parent) result = a->name + "/" + result;
        return result;
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
