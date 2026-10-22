#include <iostream>
#include <string>

int main() {
    int count = 0;
    int bestScore = 0;
    std::string bestName = "";

    std::string line = "";
    while (std::getline(std::cin, line)) {
        if (line.empty()) {
            continue;
        }
        std::size_t pos = line.find(';');
        std::string name = line.substr(0, pos);
        int score = std::stoi(line.substr(pos + 1));
        std::cout << name << " - " << score << "\n";
        if (count == 0 || score > bestScore) {
            bestScore = score;
            bestName = name;
        }
        count++;
    }

    if (count == 0) {
        std::cout << "Данных нет\n";
        return 0;
    }
    std::cout << "Лучший: " << bestName << " (" << bestScore << ")\n";
}
