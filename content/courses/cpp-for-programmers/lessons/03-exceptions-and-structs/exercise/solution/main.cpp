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
        return score >= 60;
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
    try {
        std::size_t used = 0;
        int value = std::stoi(text, &used);
        if (used != text.size() || value < 0 || value > 100) {
            return std::nullopt;
        }
        return value;
    } catch (const std::invalid_argument&) {
        return std::nullopt;
    } catch (const std::out_of_range&) {
        return std::nullopt;
    }
}

// Число студентов, лучший балл и сумма баллов. Для пустого списка все поля равны нулю.
Summary summarize(const std::vector<Student>& students) {
    Summary result;
    for (const Student& student : students) {
        result.count++;
        result.total += student.score;
        if (student.score > result.best) {
            result.best = student.score;
        }
    }
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
