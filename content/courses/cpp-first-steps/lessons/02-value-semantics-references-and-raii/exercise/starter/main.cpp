#include <algorithm>
#include <iostream>
#include <string>
#include <vector>

struct Section {
    std::string name;
    explicit Section(std::string n) : name(std::move(n)) { std::cout << "begin " << name << "\n"; }
    ~Section() { std::cout << "end " << name << "\n"; }
};

std::vector<long long> sortedCopy(std::vector<long long> v) {
    std::sort(v.begin(), v.end());
    return v;
}

// TODO: функция должна изменять вектор вызывающего кода (подумайте о типе параметра)
void shiftToZero(std::vector<long long> v) {
    long long m = *std::min_element(v.begin(), v.end());
    for (long long& x : v) x -= m;
}

void print_line(const std::string& label, const std::vector<long long>& v) {
    std::cout << label << ":";
    for (long long x : v) std::cout << " " << x;
    std::cout << "\n";
}

int main() {
    int n;
    std::cin >> n;
    std::vector<long long> data(n);
    for (long long& x : data) std::cin >> x;
    const std::vector<long long> original = data;
    {
        Section s("sorted");
        print_line("sorted", sortedCopy(data));
    }
    {
        Section s("shift");
        shiftToZero(data);
        print_line("shifted", data);
    }
    print_line("original", original);
}
