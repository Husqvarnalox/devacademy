<!-- section: simple -->
## Классы, перегрузка операторов и полиморфизм

**Класс** объединяет данные и функции над ними. Главное, что отличает C++ от Java и C#: объект класса - это значение, он занимает память там, где объявлен, и его жизнью управляют конструкторы и деструктор. Поэтому в классе важны три вопроса: как объект **создаётся** (конструкторы и список инициализации), как **ведёт себя** как встроенный тип (перегрузка операторов) и как **расширяется** (наследование).

**Перегрузка операторов** позволяет записать `a + b` или `std::cout << a` для своего типа. Это обычные функции с особыми именами (`operator+`, `operator<<`). Правило хорошего вкуса: оператор должен делать то, чего от него ждут.

**Наследование и виртуальные функции** дают полиморфизм: вы вызываете `shape->area()` через указатель или ссылку на базовый класс, а выполняется версия **реального** типа объекта. В C++ это работает только для функций, объявленных `virtual`; без `virtual` вызов определяется статическим типом переменной. Ключевое слово `override` просит компилятор проверить, что вы действительно переопределили виртуальную функцию, а `final` запрещает дальнейшее переопределение (или наследование).

Условное устройство: у объекта с виртуальными функциями есть скрытый указатель на **таблицу виртуальных функций** (vtable) его реального класса. Вызов виртуальной функции - это переход по указателю из этой таблицы.

<!-- section: precise -->
## Точные правила

**Конструкторы.**

* Члены инициализируются в порядке **объявления в классе**, а не в порядке в списке инициализации; несовпадение даёт предупреждение `-Wreorder`. Поля, не указанные в списке, инициализируются по умолчанию (для `int` - мусором), поэтому инициализируйте всё в списке или значением по умолчанию в объявлении (`int count = 0;`).
* Делегирующий конструктор вызывает другой конструктор того же класса: `Foo() : Foo(0) {}`.
* `explicit` запрещает неявное преобразование одним аргументом (`Foo f = 5;`). Ставьте его по умолчанию на однопараметрические конструкторы.
* Если вы не объявили ни одного конструктора, компилятор создаёт конструктор по умолчанию; как только объявлен любой - не создаёт (вернуть можно `= default`).
* `struct` и `class` различаются только доступом по умолчанию (`public` и `private`).

**Перегрузка операторов.** Бинарные операции, симметричные по аргументам (`+`, `==`, `<<` для потока), оформляйте свободными функциями; изменяющие объект (`+=`, `[]`, `=`) - членами. Типичная схема: `+=` как метод, `+` через `+=`. Нельзя создать новые операторы, поменять приоритет и перегрузить операторы для встроенных типов. С C++20 одной строкой `auto operator<=>(const T&) const = default;` генерируются все сравнения (`<`, `>`, `<=`, `>=`), а `bool operator==(const T&) const = default;` - равенство (проверено: в `-std=c++17` строка с `<=>` не компилируется). Не перегружайте `&&`, `||` и `,`: теряются свойства порядка вычислений.

**Наследование.** `class D : public B` моделирует «является»; приватное наследование и композиция - для «реализован через». Конструктор базы выполняется раньше конструктора производного, деструкторы - в обратном порядке. Если в конструкторе или деструкторе вызвать виртуальную функцию, выполнится версия **текущего** строящегося класса, а не производного.

**Виртуальная диспетчеризация.** Вызов `p->f()` для виртуальной `f` через указатель или ссылку на базовый класс выбирает реализацию по динамическому типу. Если объект **скопировать** в переменную базового типа (`Base b = derived;`), произойдёт **срезка** (slicing): остаётся только базовая часть, полиморфизм исчезает. Полиморфные объекты хранят по указателю (`std::unique_ptr<Base>`) и копируют через виртуальный `clone()`.

**Виртуальный деструктор.** Если класс предназначен для наследования и удаляется через указатель на базу, его деструктор обязан быть виртуальным (`virtual ~Base() = default;`). Иначе `delete` через `Base*` - неопределённое поведение: деструктор производного не вызовется. `unique_ptr<Base>` удаляет именно через `Base*`.

**`override` и `final`** (C++11). `void f() override;` - ошибка компиляции, если в базе нет виртуальной функции с точно такой же сигнатурой (например, забыли `const`). `final` у функции запрещает её переопределять, у класса - от него наследоваться. Они же дают компилятору шанс заменить виртуальный вызов прямым (девиртуализация).

**Чисто виртуальные функции.** `virtual double area() const = 0;` делает класс абстрактным: создать его объект нельзя, производный класс обязан реализовать функцию.

**Цена виртуальности.** У объекта с виртуальными функциями появляется скрытый указатель на vtable (одно слово на объект), вызов идёт по косвенной ссылке и обычно не встраивается. Для небольших и горячих функций это заметно; во всех остальных случаях выбирайте по выразительности.

**Версии.** `override` и `final` - C++11, `std::numbers::pi` и `operator<=>` - C++20 (в `-std=c++17` не компилируются).

<!-- section: examples -->
## Примеры

**Пример 1. Конструкторы: список инициализации, делегирование, explicit.**

```cpp run
#include <iostream>
#include <string>

class Account {
public:
    Account() : Account("anonymous", 0) {}
    explicit Account(std::string owner) : Account(std::move(owner), 0) {}
    Account(std::string owner, int cents) : owner_(std::move(owner)), cents_(cents) { ++created; }

    void deposit(int cents) { cents_ += cents; }
    int balance() const { return cents_; }
    const std::string& owner() const { return owner_; }
    static inline int created = 0;

private:
    std::string owner_;
    int cents_ = 0;
};

void show(const Account& a) { std::cout << a.owner() << ": " << a.balance() << "\n"; }

int main() {
    Account a;
    Account b("alice");
    Account c("bob", 150);
    c.deposit(50);
    show(a);
    show(b);
    show(c);
    std::cout << "created " << Account::created << "\n";
    // show("carol");  // не компилируется: конструктор explicit
}
```
```output
anonymous: 0
alice: 0
bob: 200
created 3
```

**Пример 2. Перегрузка операторов и сравнение по умолчанию (C++20).**

```cpp run
#include <algorithm>
#include <compare>
#include <iostream>
#include <vector>

struct Vec2 {
    int x = 0, y = 0;

    Vec2& operator+=(const Vec2& o) {
        x += o.x;
        y += o.y;
        return *this;
    }
    Vec2 operator-() const { return {-x, -y}; }
    auto operator<=>(const Vec2&) const = default;  // даёт ==, !=, <, >, <=, >=
};

Vec2 operator+(Vec2 a, const Vec2& b) { return a += b; }
Vec2 operator*(int k, const Vec2& v) { return {k * v.x, k * v.y}; }

std::ostream& operator<<(std::ostream& out, const Vec2& v) { return out << "(" << v.x << ", " << v.y << ")"; }

int main() {
    Vec2 a{1, 2}, b{3, -1};
    std::cout << a + b << " " << 3 * a << " " << -b << "\n";
    std::cout << (a == Vec2{1, 2}) << " " << (a < b) << " " << (b < a) << "\n";
    std::vector<Vec2> pts{{2, 1}, {1, 5}, {1, 2}};
    std::sort(pts.begin(), pts.end());
    for (const Vec2& p : pts) std::cout << p << " ";
    std::cout << "\n";
}
```
```output
(4, 1) (3, 6) (-3, 1)
1 1 0
(1, 2) (1, 5) (2, 1) 
```

Сравнение генерируется по полям в порядке объявления: сначала `x`, потом `y`.

**Пример 3. Виртуальная диспетчеризация, срезка и `override`.**

```cpp run
#include <iostream>
#include <memory>
#include <string>
#include <vector>

class Animal {
public:
    virtual ~Animal() = default;
    virtual std::string sound() const { return "..."; }
    std::string describe() const { return "animal says " + sound(); }  // невиртуальная, но вызывает виртуальную
};

class Dog : public Animal {
public:
    std::string sound() const override { return "woof"; }
};

class Cat final : public Animal {
public:
    std::string sound() const override { return "meow"; }
};

void byValue(Animal a) { std::cout << "by value: " << a.sound() << "\n"; }
void byRef(const Animal& a) { std::cout << "by ref: " << a.sound() << "\n"; }

int main() {
    std::vector<std::unique_ptr<Animal>> zoo;
    zoo.push_back(std::make_unique<Dog>());
    zoo.push_back(std::make_unique<Cat>());
    zoo.push_back(std::make_unique<Animal>());
    for (const auto& a : zoo) std::cout << a->describe() << "\n";

    Dog d;
    byRef(d);
    byValue(d);  // срезка: остаётся только часть Animal
}
```
```output
animal says woof
animal says meow
animal says ...
by ref: woof
by value: ...
```

`describe()` невиртуальна, но внутри вызывает виртуальную `sound()`, и работает версия реального типа. При передаче по значению `Dog` превращается в `Animal` - срезка.

**Пример 4. Виртуальная функция в конструкторе и порядок вызовов.**

```cpp run
#include <iostream>
#include <memory>

class Base {
public:
    Base() { std::cout << "Base ctor, "; whoAmI(); }
    virtual ~Base() { std::cout << "Base dtor, "; whoAmI(); }
    virtual void whoAmI() const { std::cout << "Base::whoAmI\n"; }
};

class Derived : public Base {
public:
    Derived() { std::cout << "Derived ctor, "; whoAmI(); }
    ~Derived() override { std::cout << "Derived dtor, "; whoAmI(); }
    void whoAmI() const override { std::cout << "Derived::whoAmI\n"; }
};

int main() {
    std::unique_ptr<Base> p = std::make_unique<Derived>();
    std::cout << "---\n";
    p->whoAmI();
    std::cout << "---\n";
}
```
```output
Base ctor, Base::whoAmI
Derived ctor, Derived::whoAmI
---
Derived::whoAmI
---
Derived dtor, Derived::whoAmI
Base dtor, Base::whoAmI
```

Внутри конструктора `Base` объект ещё не стал `Derived`, поэтому вызвана версия `Base`. В деструкторе - наоборот: часть `Derived` уже уничтожена.

**Пример 5. Абстрактный класс и `clone`.**

```cpp run
#include <iostream>
#include <memory>
#include <vector>

class Shape {
public:
    virtual ~Shape() = default;
    virtual double area() const = 0;
    virtual std::unique_ptr<Shape> clone() const = 0;
};

class Square final : public Shape {
public:
    explicit Square(double s) : s_(s) {}
    double area() const override { return s_ * s_; }
    std::unique_ptr<Shape> clone() const override { return std::make_unique<Square>(*this); }

private:
    double s_;
};

int main() {
    std::vector<std::unique_ptr<Shape>> a;
    a.push_back(std::make_unique<Square>(3));
    std::vector<std::unique_ptr<Shape>> b;
    for (const auto& s : a) b.push_back(s->clone());  // глубокая копия полиморфной коллекции
    std::cout << a.size() << " " << b.size() << " " << b[0]->area() << " " << (a[0] != b[0]) << "\n";
    // Shape s;  // не компилируется: класс абстрактный
}
```
```output
1 1 9 1
```

**Пример 6. Скрытая цена: указатель на vtable.**

```cpp run
#include <iostream>

struct Plain {
    int x;
    int get() const { return x; }
};

struct Virt {
    int x;
    virtual int get() const { return x; }
    virtual ~Virt() = default;
};

int main() {
    std::cout << (sizeof(Virt) > sizeof(Plain)) << "\n";
    std::cout << sizeof(Plain) << "\n";
}
```
```output
1
4
```

`Virt` больше на размер скрытого указателя (с учётом выравнивания). Точный размер зависит от платформы; здесь важно сравнение.

**Пример 7. Ошибки, которые ловят `override` и `final` (не компилируется).**

```cpp
struct B {
    virtual void f() const;
    virtual void g();
};
struct D : B {
    void f() override;            // ошибка: нет const, это другая функция, а не переопределение
    void g() final;
};
struct E final {};
struct G : E {};                  // ошибка: нельзя наследовать от final-класса
struct F : D {
    void g() override;            // ошибка: D::g объявлена final
};
```

Без `override` первая строка молча создала бы новую функцию, и `B* p = new D; p->f()` вызвал бы версию базы.

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть `virtual` у деструктора базы.** `delete` производного через `Base*` - неопределённое поведение (деструктор производного не вызывается).
* **Не писать `override`.** Опечатка в имени или `const` создаёт новую функцию, а не переопределение; ошибка видна только по поведению.
* **Срезка.** Передача или хранение полиморфных объектов по значению (`std::vector<Animal>`) отрезает производную часть.
* **Вызывать виртуальные функции в конструкторе и деструкторе, рассчитывая на производную версию.** Вызывается версия текущего класса.
* **Не инициализировать члены.** Поля встроенных типов остаются неопределёнными; используйте список инициализации или инициализаторы в объявлении.
* **Порядок в списке инициализации не равен порядку инициализации.** Поле, зависящее от другого, может использовать его до инициализации; смотрите на предупреждение `-Wreorder`.
* **Без `explicit` на однопараметрическом конструкторе.** Неожиданные неявные преобразования (`show("carol")` создаёт `Account`).
* **Перегружать оператор ради «красоты».** `operator+` для не сложения, `operator*` для не умножения запутывают читателя.
* **Наследование ради повторного использования кода.** Если связь не «является», берите композицию.

<!-- section: summary -->
## Итоги

* Конструкторы строят объект списком инициализации в порядке объявления полей; `explicit` по умолчанию на однопараметрических.
* Операторы - обычные функции; симметричные пишут свободными, изменяющие - членами; в C++20 сравнения генерируются через `<=>` по умолчанию.
* Полиморфизм работает через указатель или ссылку на базу и только для `virtual`; всегда добавляйте `override`, `final` закрывает ветку, у полиморфной базы виртуальный деструктор.
* Объект с виртуальными функциями несёт скрытый указатель на vtable; передача по значению приводит к срезке.

Дальше: [шаблоны, концепты и constexpr](lesson:cpp.fundamentals.templates-concepts-constexpr).
Назад: [RAII и умные указатели](lesson:cpp.fundamentals.raii-smart-pointers).
Сравните: [интерфейсы и наследование в C#](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism), [интерфейсы и sealed-типы в Java](lesson:java.fundamentals.interfaces-sealed-patterns), [интерфейсы в Go](lesson:go.fundamentals.interfaces), [трейты в Rust](lesson:rust.fundamentals.traits-and-generics).
