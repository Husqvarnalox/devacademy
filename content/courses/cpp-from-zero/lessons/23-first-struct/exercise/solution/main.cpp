#include <iostream>
#include <string>
#include <vector>

struct Student {
    std::string name = "";
    int score = 0;

    std::string grade() const {
        if (score >= 90) {
            return "отлично";
        }
        if (score >= 75) {
            return "хорошо";
        }
        if (score >= 60) {
            return "зачёт";
        }
        return "незачёт";
    }

    bool passed() const {
        return score >= 60;
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
