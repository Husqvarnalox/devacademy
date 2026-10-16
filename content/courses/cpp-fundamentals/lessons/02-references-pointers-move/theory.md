<!-- section: simple -->
## Как передавать, хранить и «забирать» объекты

В первом курсе вы видели, что `=` и передача по значению **копируют**. Копирование безопасно, но для вектора на миллион элементов дорого. В этом уроке - инструменты, которые позволяют обойтись без лишних копий и при этом не потерять контроль над владением:

* **Ссылка** `T&` - второе имя существующего объекта. **Указатель** `T*` - отдельный объект, хранящий адрес; он может быть пустым (`nullptr`) и может быть перенаправлен.
* **`const`** - обещание не менять. Компилятор проверяет его, поэтому `const` в сигнатуре - это документация, которой можно доверять.
* **Категории значений.** Выражение либо *именованное и постоянное* (lvalue: `x`), либо *временное* (rvalue: `x + 1`, `f()`). Временное можно не копировать, а **забрать** его содержимое.
* **Перемещение** (`std::move`, move-конструктор) - передача ресурсов объекта новому владельцу вместо копирования. Исходный объект остаётся жив, но «пустым».
* **Правила нуля, трёх и пяти** отвечают на вопрос: что делать классу, который владеет ресурсом.

Практическое правило: если класс состоит только из `std::string`, `std::vector`, `std::unique_ptr` и других типов, умеющих копироваться и перемещаться сами, **не пишите** деструктор и операции копирования и перемещения (правило нуля).

<!-- section: precise -->
## Точные правила

**Ссылки.** Ссылка обязана быть инициализирована, не может быть пустой и не перепривязывается: присваивание `r = y` меняет объект, на который ссылка указывает. Ссылка на объект, который уже уничтожен, - висячая, обращение к ней - неопределённое поведение.

**Указатели и const.** `const int* p` - указатель на неизменяемое значение (сам `p` можно перенаправить); `int* const p` - неизменяемый указатель на изменяемое значение; `const int* const p` - оба неизменяемы. Читайте объявление справа налево. Разыменование `nullptr` - неопределённое поведение, поэтому перед использованием проверяйте. Арифметика указателей допустима только внутри массива (и на один элемент после последнего).

**const-корректность.** Метод, не меняющий объект, помечают `const`: `size_t size() const`. Для `const`-объекта доступны только такие методы. Метод можно перегрузить по `const`: `T& at(i)` и `const T& at(i) const`. Ключевое слово `mutable` разрешает менять отдельное поле в `const`-методе (кэш, счётчик обращений). `const_cast`, снимающий `const` с объекта, который изначально был константным, и последующая запись - неопределённое поведение.

**Категории значений (C++11 и новее).** Каждое выражение имеет тип и категорию:

* **lvalue** - обозначает объект с идентичностью: имя переменной, `*p`, `v[i]`, а также имя ссылки (в том числе `int&& r` - само по себе выражение `r` является lvalue);
* **prvalue** - «чистое» временное значение: литерал `3`, `x + 1`, результат функции, возвращающей по значению;
* **xvalue** - объект, ресурсы которого можно забрать: результат `std::move(x)`.

Категории lvalue и xvalue вместе образуют glvalue; prvalue и xvalue - rvalue. Ссылка `T&&` (rvalue-ссылка) привязывается к rvalue, `const T&` - к чему угодно. Перегрузки по `T&`, `const T&` и `T&&` выбираются по категории аргумента.

**`std::move` не перемещает.** Это приведение типа к `T&&` (xvalue) и ничего больше. Перемещение выполняет move-конструктор или move-присваивание, которые принимают `T&&`. Если у типа их нет, а есть копирующие, вызовутся они. `std::move` константного объекта даёт `const T&&`, который привязывается к копирующему конструктору, поэтому такой объект копируется.

**Состояние после перемещения.** Стандартные типы после перемещения остаются в допустимом, но не определённом состоянии: можно уничтожить объект или присвоить ему новое значение, а рассчитывать на его содержимое нельзя. Собственные классы обязаны сохранять инвариант: перемещённый объект должен корректно уничтожаться и присваиваться.

**Гарантированное исключение копирования (C++17).** Инициализация объекта prvalue того же типа (`T x = T(...)`, возврат `return T(...)`) не создаёт ни копии, ни перемещения: объект строится сразу на месте. Для именованных локальных переменных (`return local;`) компилятор вправе применить NRVO, но не обязан; иногда из них неявно выполняется перемещение. Поэтому не пишите `return std::move(local);`: это мешает оптимизации.

**Правила нуля, трёх и пяти.**

* *Правило трёх:* если классу нужен собственный деструктор, конструктор копирования или копирующее присваивание (обычно потому, что он владеет ресурсом), скорее всего нужны все три.
* *Правило пяти:* к ним добавляются конструктор перемещения и перемещающее присваивание. Если вы объявили деструктор или любую операцию копирования, **неявные операции перемещения не создаются**, и `std::move` молча превращается в копирование.
* *Правило нуля:* держите ресурсы в членах-владельцах (`vector`, `string`, `unique_ptr`) и не объявляйте ни одну из пяти операций.

Операции перемещения помечайте `noexcept`. `std::vector` при увеличении ёмкости перемещает элементы только если move-конструктор `noexcept` (или копирование невозможно); иначе он копирует, чтобы сохранить сильную гарантию исключений.

**Версии.** Rvalue-ссылки и перемещение появились в C++11 (`-std=c++03 -pedantic-errors` отклоняет `&&`), `std::exchange` - в C++14 (в `-std=c++11` не компилируется), гарантированное исключение копирования - в C++17 (тип с удалёнными копированием и перемещением можно вернуть из функции в `-std=c++17`, а в `-std=c++14` - нет).

<!-- section: examples -->
## Примеры

**Пример 1. Ссылка, указатель, const.**

```cpp run
#include <iostream>

int main() {
    int x = 10;
    int& r = x;
    r += 5;
    int* p = &x;
    *p += 1;
    std::cout << x << "\n";

    int y = 100;
    const int* toConst = &x;  // нельзя менять *toConst, но сам указатель переназначается
    toConst = &y;
    int* const fixed = &x;    // указатель нельзя переназначить, значение менять можно
    *fixed = 0;
    std::cout << *toConst << " " << x << "\n";

    int* nothing = nullptr;
    if (nothing == nullptr) std::cout << "null checked\n";
}
```
```output
16
100 0
null checked
```

Попытки `*toConst = 1;` или `fixed = &y;` не компилируются: `cannot assign to variable ... with const-qualified type`.

**Пример 2. Какая перегрузка выбирается для разных выражений.**

```cpp run
#include <iostream>
#include <utility>

void show(int&) { std::cout << "lvalue\n"; }
void show(const int&) { std::cout << "const lvalue\n"; }
void show(int&&) { std::cout << "rvalue\n"; }

int make() { return 7; }

int main() {
    int a = 1;
    const int c = 2;
    show(a);
    show(c);
    show(3);
    show(a + 1);
    show(make());
    show(std::move(a));
    int&& r = 3;
    show(r);
}
```
```output
lvalue
const lvalue
rvalue
rvalue
rvalue
rvalue
lvalue
```

Последняя строка - главная ловушка: `r` объявлена как rvalue-ссылка, но выражение `r` - это имя, то есть lvalue.

**Пример 3. Перегрузка по const.**

```cpp run
#include <iostream>
#include <vector>

class Buffer {
public:
    int& at(std::size_t i) {
        std::cout << "non-const ";
        return data_[i];
    }
    const int& at(std::size_t i) const {
        std::cout << "const ";
        return data_[i];
    }

private:
    std::vector<int> data_{1, 2, 3};
};

void readOnly(const Buffer& b) { std::cout << b.at(1) << "\n"; }

int main() {
    Buffer buf;
    buf.at(0) = 10;
    std::cout << buf.at(0) << "\n";
    readOnly(buf);
}
```
```output
non-const non-const 10
const 2
```

Через `const Buffer&` компилятор выбирает `const`-версию и не даст присвоить результат.

**Пример 4. `std::move` константного объекта копирует.**

```cpp run
#include <iostream>
#include <string>
#include <utility>

int main() {
    const std::string source(40, 'a');
    std::string target = std::move(source);
    std::cout << source.size() << " " << target.size() << "\n";

    std::string movable(40, 'b');
    std::string taken = std::move(movable);
    std::cout << taken.size() << "\n";
}
```
```output
40 40
40
```

`source` остался нетронутым: `const std::string&&` привязался к копирующему конструктору. Для `movable` содержимое действительно передано в `taken`.

**Пример 5. Пять специальных функций, одна за другой.**

```cpp run
#include <iostream>
#include <string>
#include <utility>

struct Tracked {
    std::string name;
    explicit Tracked(std::string n) : name(std::move(n)) { std::cout << "ctor " << name << "\n"; }
    Tracked(const Tracked& o) : name(o.name + "'") { std::cout << "copy " << o.name << "\n"; }
    Tracked(Tracked&& o) noexcept : name(std::move(o.name)) { std::cout << "move " << name << "\n"; }
    Tracked& operator=(const Tracked& o) {
        name = o.name + "=";
        std::cout << "copy-assign " << o.name << "\n";
        return *this;
    }
    Tracked& operator=(Tracked&& o) noexcept {
        name = std::move(o.name);
        std::cout << "move-assign " << name << "\n";
        return *this;
    }
    ~Tracked() { std::cout << "dtor\n"; }
};

Tracked make(const char* n) { return Tracked(n); }

int main() {
    Tracked a("a");
    Tracked b = a;
    Tracked c = std::move(a);
    b = c;
    c = std::move(b);
    Tracked d = make("d");
    std::cout << "end\n";
}
```
```output
ctor a
copy a
move a
copy-assign a
move-assign a=
ctor d
end
dtor
dtor
dtor
dtor
```

Вызов `make("d")` не напечатал ни `copy`, ни `move`: с C++17 временный объект строится прямо в `d`. Четыре `dtor` в конце - по одному на `d`, `c`, `b` и `a` (в обратном порядке создания).

**Пример 6. `noexcept` решает, будет ли `vector` перемещать при росте.**

```cpp run
#include <iostream>
#include <vector>

struct Quiet {
    static inline int copies = 0, moves = 0;
    Quiet() = default;
    Quiet(const Quiet&) { ++copies; }
    Quiet(Quiet&&) noexcept { ++moves; }
};

struct Loud {
    static inline int copies = 0, moves = 0;
    Loud() = default;
    Loud(const Loud&) { ++copies; }
    Loud(Loud&&) { ++moves; }  // без noexcept
};

template <typename T>
void grow(const char* label) {
    std::vector<T> v;
    v.reserve(2);
    v.emplace_back();
    v.emplace_back();
    v.emplace_back();  // третий элемент вызывает перевыделение: два старых переезжают
    std::cout << label << ": copies=" << T::copies << " moves=" << T::moves << "\n";
}

int main() {
    grow<Quiet>("noexcept move");
    grow<Loud>("throwing move");
}
```
```output
noexcept move: copies=0 moves=2
throwing move: copies=2 moves=0
```

Без `noexcept` вектор предпочёл копирование: оно безопасно для исключений. Забытое `noexcept` на move-конструкторе - частая причина «странной» медлительности.

**Пример 7. Правило нуля.**

```cpp run
#include <iostream>
#include <string>
#include <type_traits>
#include <vector>

struct Student {
    std::string name;
    std::vector<int> grades;
};

int main() {
    Student a{"Ada", {5, 4, 5}};
    Student b = a;              // копия, написанная компилятором
    Student c = std::move(a);   // перемещение, написанное компилятором
    b.grades.push_back(3);
    std::cout << b.name << " " << b.grades.size() << " " << c.grades.size() << "\n";
    std::cout << std::is_nothrow_move_constructible_v<Student> << "\n";
}
```
```output
Ada 4 3
1
```

**Пример 8. Висячая ссылка и ошибки const (не запускать).**

```cpp
const std::string& bad() {
    std::string local = "temporary";
    return local;            // ссылка на объект, который умирает при выходе: UB при использовании
}

void edit(const std::vector<int>& v) {
    v.push_back(1);          // ошибка компиляции: v константный
}
```

Компилятор обычно предупреждает о первом случае (`reference to stack memory associated with local variable returned`), второй не компилируется.

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `std::move` что-то перемещает.** Это только приведение типа. Без move-конструктора или с `const`-объектом выполнится копирование.
* **Использовать объект после `std::move`.** Допустимы уничтожение и присваивание; на прежнее значение рассчитывать нельзя.
* **`return std::move(local);`.** Отключает исключение копирования; пишите `return local;`.
* **Объявить деструктор и ждать автоматических перемещений.** Пользовательский деструктор или копирование отменяет неявные move-операции: класс тихо копируется.
* **Забыть `noexcept` у move-конструктора.** `std::vector` будет копировать элементы при росте.
* **Путать `const int*` и `int* const`.** Читайте справа налево: «указатель на const int» против «const-указатель на int».
* **Считать, что `T&&` в списке параметров всегда rvalue.** Внутри функции имя такого параметра - lvalue; чтобы передать дальше как rvalue, нужен `std::move`.
* **Хранить ссылку или указатель на локальный объект после выхода из функции.** Объект уничтожен: неопределённое поведение.
* **Снимать `const` через `const_cast` и писать.** Если объект изначально константный, запись - неопределённое поведение.

<!-- section: summary -->
## Итоги

* Ссылка - второе имя объекта, указатель - отдельный объект с адресом; `const` проверяется компилятором, читайте объявления справа налево.
* Выражения делятся на lvalue, prvalue и xvalue; `std::move` лишь превращает lvalue в xvalue, а перемещает move-конструктор.
* Правило нуля предпочтительно; если пишете одну из пяти операций, подумайте обо всех, помечайте перемещение `noexcept`.
* С C++17 инициализация из prvalue не копирует и не перемещает.

Дальше: [RAII и умные указатели](lesson:cpp.fundamentals.raii-smart-pointers).
Назад: [модель компиляции и время жизни](lesson:cpp.fundamentals.compilation-lifetime-storage).
Сравните: [владение и перемещение в Rust](lesson:rust.fundamentals.ownership-and-moves-in-depth), [заимствование и времена жизни в Rust](lesson:rust.fundamentals.borrowing-and-lifetimes), [классы, записи и структуры в C#](lesson:csharp.fundamentals.classes-records-structs), [срезы и карты в Go](lesson:go.fundamentals.maps-and-slices-deep-dive).
