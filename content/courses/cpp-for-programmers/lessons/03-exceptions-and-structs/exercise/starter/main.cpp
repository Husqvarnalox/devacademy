#include <iomanip>
#include <iostream>
#include <optional>
#include <stdexcept>
#include <string>
#include <vector>

struct Student {
    std::string name;
    int score = 0;

    // Зачёт, если балл не меньше 60.
    bool passed() const {
        // TODO: вернуть true, если score не меньше 60
        return false;
    }
};

struct Summary {
    int count = 0;
    int best = 0;
    int total = 0;
};

// Строка допустима, если std::stoi разобрал её целиком и результат лежит в диапазоне 0..100.
// Иначе - пустой optional (исключения наружу не выходят).
std::optional<int> parseScore(const std::string& text) {
    // TODO: std::stoi(text, &used) разбирает число и записывает, сколько символов использовано;
    // исключения invalid_argument и out_of_range нужно перехватить
    if (text.empty()) {
        return std::nullopt;
    }
    return std::stoi(text);
}

// Число студентов, лучший балл и сумма баллов. Для пустого списка все поля равны нулю.
Summary summarize(const std::vector<Student>& students) {
    // TODO: пройдите по students и накопите count, total и best
    Summary result;
    result.count = static_cast<int>(students.size());
    return result;
}

int main() {
    std::vector<Student> students;
    int bad = 0;
    std::string name;
    std::string text;
    while (std::cin >> name >> text) {
        std::optional<int> score = parseScore(text);
        if (!score) {
            std::cout << "bad: " << name << "\n";
            bad++;
            continue;
        }
        Student student{name, *score};
        std::cout << name << ": " << (student.passed() ? "pass" : "fail") << "\n";
        students.push_back(student);
    }
    Summary summary = summarize(students);
    double average = summary.count == 0 ? 0.0 : static_cast<double>(summary.total) / summary.count;
    std::cout << "count=" << summary.count << " best=" << summary.best << " avg=" << std::fixed << std::setprecision(2)
              << average << " bad=" << bad << "\n";
}
