#include <iostream>
#include <string>
#include <vector>

struct Student {
    std::string name = "";
    int score = 0;

    std::string grade() const {
        // TODO: верните "отлично", "хорошо", "зачёт" или "незачёт" по баллу
        return "";
    }

    bool passed() const {
        // TODO: верните true, если балл не меньше 60
        return false;
    }
};

int main() {
    std::vector<Student> students;
    std::string name = "";
    int score = 0;
    while (std::cin >> name >> score) {
        Student s = {name, score};
        students.push_back(s);
    }

    int passedCount = 0;
    for (const Student& s : students) {
        std::cout << s.name << ": " << s.score << " - " << s.grade() << "\n";
        if (s.passed()) {
            passedCount++;
        }
    }
    std::cout << "Сдали: " << passedCount << " из " << students.size() << "\n";
}
