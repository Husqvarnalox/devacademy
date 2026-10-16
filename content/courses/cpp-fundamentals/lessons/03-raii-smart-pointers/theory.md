<!-- section: simple -->
## Кто владеет объектом, тот его и убирает

В C++ нет сборщика мусора. Вместо него действует простая идея: **у каждого ресурса (память, файл, мьютекс, соединение) есть владелец, и ресурс освобождается, когда умирает владелец.** Это называется **RAII** (Resource Acquisition Is Initialization): ресурс захватывается в конструкторе объекта-владельца и освобождается в его деструкторе. Деструктор вызывается автоматически на любом пути выхода из области видимости: через `return`, через `break` и через исключение.

Для динамической памяти стандартная библиотека даёт **умные указатели**, которые являются такими объектами-владельцами:

* **`std::unique_ptr<T>`** - единоличный владелец. Копировать нельзя, можно только **передать** владение (`std::move`). Накладные расходы практически нулевые. Это выбор по умолчанию.
* **`std::shared_ptr<T>`** - совместное владение со счётчиком ссылок. Объект умирает, когда исчезает последний `shared_ptr`.
* **`std::weak_ptr<T>`** - наблюдатель за объектом `shared_ptr`: не продлевает ему жизнь, умеет проверить, жив ли объект, и временно получить владение.

Правило: **владеет - умный указатель или контейнер, наблюдает - обычная ссылка или сырой указатель.** Сырой `T*` в современном C++ означает «я не отвечаю за освобождение».

Вручную `new` и `delete` в прикладном коде почти не нужны: `std::make_unique<T>(...)` и `std::make_shared<T>(...)` создают объект и сразу отдают его владельцу.

<!-- section: precise -->
## Точные правила

**RAII и исключения.** Деструктор автоматического объекта вызывается при размотке стека, поэтому ресурс освобождается и при исключении. Если конструктор объекта бросил исключение, деструктор самого объекта не вызывается (объект не был создан), но деструкторы уже созданных членов и базовых классов вызываются. Деструктор по умолчанию `noexcept`; выбрасывать исключения из деструктора нельзя: при размотке это приводит к `std::terminate`.

**`unique_ptr`.**

* Копирование удалено, перемещение передаёт владение; после перемещения исходный `unique_ptr` гарантированно пуст (`nullptr`).
* Параметр `std::unique_ptr<T>` по значению означает «функция забирает владение» (sink); `T*` или `T&` означают «просто использует»; возврат `unique_ptr` по значению - фабрика, передающая владение вызывающему.
* `std::make_unique` появился в C++14 (в `-std=c++11` не компилируется). Он безопаснее `unique_ptr<T>(new T)`: нет утечки при исключении между `new` и построением указателя.
* Второй шаблонный параметр - **удалитель** (deleter): `std::unique_ptr<T, D>` вызывает `D` вместо `delete`. Так в RAII-обёртку превращают `FILE*`, дескрипторы и другие C-ресурсы. Вариант `unique_ptr<T[]>` вызывает `delete[]`.
* Методы: `get()` (сырой указатель без передачи владения), `release()` (отдать владение, не освобождая), `reset(p)` (освободить текущий и принять новый).

**`shared_ptr`.**

* Хранит указатель на объект и на **управляющий блок** со счётчиками (сильных и слабых ссылок). Копирование увеличивает счётчик, уничтожение - уменьшает; при нуле сильных ссылок объект уничтожается. Операции со счётчиком атомарные, поэтому разные копии `shared_ptr` можно использовать из разных потоков, но сам объект, на который они указывают, не становится потокобезопасным.
* `std::make_shared<T>(...)` выделяет объект и управляющий блок за одну аллокацию. `shared_ptr<T[]>` появился в C++17, `make_shared<T[]>` - в C++20 (проверено ключами `-std`).
* `use_count()` показывает число владельцев. Счётчик нельзя использовать для логики синхронизации.
* **Циклы.** Если A хранит `shared_ptr` на B, а B - `shared_ptr` на A, счётчики никогда не обнулятся: утечка. Обратную ссылку делают `weak_ptr`.

**`weak_ptr`.** Создаётся из `shared_ptr`, не увеличивает счётчик владельцев. `expired()` сообщает, умер ли объект; `lock()` возвращает `shared_ptr` (пустой, если объект уничтожен) и безопасно продлевает жизнь на время использования. Проверять `expired()` и затем вызывать `lock()` не нужно: между вызовами объект может умереть в другом потоке, достаточно одного `lock()` и проверки результата.

**Что выбрать.** По умолчанию - значение (член или локальная переменная). Нужна динамическая продолжительность жизни или полиморфизм - `unique_ptr`. Объект действительно делят несколько владельцев с неочевидным порядком освобождения - `shared_ptr` (это редкость, а не норма). Не владеете - `T&` или `T*`.

**Порядок уничтожения элементов контейнера.** Стандарт не фиксирует, в каком порядке `std::vector` уничтожает свои элементы. Не стройте логику на порядке деструкторов элементов контейнера; для проверки используйте счётчики.

<!-- section: examples -->
## Примеры

**Пример 1. RAII-охранник срабатывает и при исключении.**

```cpp run
#include <iostream>
#include <stdexcept>
#include <string>

class LogGuard {
public:
    explicit LogGuard(std::string what) : what_(std::move(what)) { std::cout << "enter " << what_ << "\n"; }
    ~LogGuard() { std::cout << "leave " << what_ << "\n"; }
    LogGuard(const LogGuard&) = delete;
    LogGuard& operator=(const LogGuard&) = delete;

private:
    std::string what_;
};

void risky(bool fail) {
    LogGuard g("risky");
    if (fail) throw std::runtime_error("boom");
    std::cout << "work done\n";
}

int main() {
    risky(false);
    try {
        risky(true);
    } catch (const std::exception& e) {
        std::cout << "caught " << e.what() << "\n";
    }
}
```
```output
enter risky
work done
leave risky
enter risky
leave risky
caught boom
```

Деструктор вызван в обоих случаях, при исключении - до входа в `catch`.

**Пример 2. Передача владения `unique_ptr`.**

```cpp run
#include <iostream>
#include <memory>
#include <string>

struct Document {
    std::string title;
    explicit Document(std::string t) : title(std::move(t)) { std::cout << "open " << title << "\n"; }
    ~Document() { std::cout << "close " << title << "\n"; }
};

std::unique_ptr<Document> create(const std::string& title) {
    return std::make_unique<Document>(title);  // фабрика передаёт владение вызывающему
}

void inspect(const Document& d) { std::cout << "inspect " << d.title << "\n"; }  // только использует

void consume(std::unique_ptr<Document> d) {  // забирает владение
    std::cout << "consume " << d->title << "\n";
}  // документ закрывается здесь

int main() {
    auto doc = create("report");
    inspect(*doc);
    std::unique_ptr<Document> other = std::move(doc);
    std::cout << (doc == nullptr) << " " << (other != nullptr) << "\n";
    consume(std::move(other));
    std::cout << "after consume\n";
}
```
```output
open report
inspect report
1 1
consume report
close report
after consume
```

После `std::move` исходный `unique_ptr` гарантированно пуст. Документ закрылся внутри `consume`, где умер последний владелец.

**Пример 3. Собственный удалитель для «чужого» ресурса.**

```cpp run
#include <iostream>
#include <memory>

int main() {
    auto closeHandle = [](int* h) {
        std::cout << "close handle " << *h << "\n";
        delete h;
    };
    {
        std::unique_ptr<int, decltype(closeHandle)> handle(new int(42), closeHandle);
        std::cout << "using handle " << *handle << "\n";
    }
    std::cout << "done\n";
}
```
```output
using handle 42
close handle 42
done
```

Так же оборачивают `FILE*` (`fclose`), сокеты и любые пары «открыть/закрыть».

**Пример 4. Счётчик `shared_ptr`.**

```cpp run
#include <iostream>
#include <memory>
#include <vector>

struct Config {
    ~Config() { std::cout << "config destroyed\n"; }
};

int main() {
    auto cfg = std::make_shared<Config>();
    std::cout << cfg.use_count() << "\n";
    {
        std::vector<std::shared_ptr<Config>> users(3, cfg);
        std::cout << cfg.use_count() << "\n";
    }
    std::cout << cfg.use_count() << "\n";
    cfg.reset();
    std::cout << "end\n";
}
```
```output
1
4
1
config destroyed
end
```

**Пример 5. Цикл из `shared_ptr` и `weak_ptr`.**

```cpp run
#include <iostream>
#include <memory>

struct Strong {
    std::shared_ptr<Strong> other;
    ~Strong() { std::cout << "Strong destroyed\n"; }
};

struct Weak {
    std::weak_ptr<Weak> other;
    ~Weak() { std::cout << "Weak destroyed\n"; }
};

int main() {
    auto a = std::make_shared<Strong>();
    auto b = std::make_shared<Strong>();
    a->other = b;
    b->other = a;
    std::cout << "strong counts: " << a.use_count() << " " << b.use_count() << "\n";
    a->other.reset();  // разрываем цикл вручную, иначе обе утекли бы

    auto c = std::make_shared<Weak>();
    auto d = std::make_shared<Weak>();
    c->other = d;
    d->other = c;
    std::cout << "weak counts: " << c.use_count() << " " << d.use_count() << "\n";
    std::cout << "end of main\n";
}
```
```output
strong counts: 2 2
weak counts: 1 1
end of main
Weak destroyed
Weak destroyed
Strong destroyed
Strong destroyed
```

В цикле из сильных ссылок счётчики равны 2 и без ручного `reset()` ни один деструктор бы не вызвался. С `weak_ptr` счётчики остаются 1, и объекты умирают сами при выходе из `main`.

**Пример 6. `weak_ptr` как кэш, который не продлевает жизнь.**

```cpp run
#include <iostream>
#include <memory>
#include <string>

struct Texture {
    std::string name;
    explicit Texture(std::string n) : name(std::move(n)) { std::cout << "load " << name << "\n"; }
    ~Texture() { std::cout << "unload " << name << "\n"; }
};

int main() {
    std::weak_ptr<Texture> cache;
    {
        auto owner = std::make_shared<Texture>("brick");
        cache = owner;
        if (auto alive = cache.lock()) std::cout << "cached " << alive->name << " owners=" << alive.use_count() << "\n";
    }
    std::cout << "expired: " << cache.expired() << "\n";
    if (auto alive = cache.lock()) std::cout << "still there\n";
    else std::cout << "reload needed\n";
}
```
```output
load brick
cached brick owners=2
unload brick
expired: 1
reload needed
```

**Пример 7. Типичные ошибки владения (не запускать).**

```cpp
Widget* w = new Widget;
delete w;
delete w;                       // двойное освобождение: неопределённое поведение

Widget* make() { return new Widget; }   // кто освободит? Вернуть unique_ptr<Widget>

std::unique_ptr<int> a = std::make_unique<int>(1);
std::unique_ptr<int> b = a;     // ошибка компиляции: копирование unique_ptr удалено

int* raw = a.get();
a.reset();
*raw = 2;                       // raw висит: неопределённое поведение
```

<!-- section: mistakes -->
## Типичные ошибки

* **Голые `new` и `delete`.** Любой `return` или исключение между ними приводит к утечке. Используйте `make_unique`.
* **Двойное владение одним сырым указателем.** Два `unique_ptr` (или `unique_ptr` и `delete`) на один объект - двойное освобождение.
* **`shared_ptr` «на всякий случай».** Совместное владение усложняет рассуждения о времени жизни и стоит дороже; сначала подумайте, нельзя ли обойтись `unique_ptr`.
* **Цикл из `shared_ptr`.** Объекты никогда не уничтожатся. Обратные и «наблюдательные» ссылки делайте `weak_ptr` (или сырым указателем, если время жизни гарантировано).
* **Создавать два `shared_ptr` от одного сырого указателя.** `shared_ptr<T> a(p), b(p);` создаёт два независимых управляющих блока и двойное освобождение. Копируйте уже существующий `shared_ptr`.
* **Хранить результат `get()` дольше, чем живёт владелец.** Сырой указатель после `reset()` или уничтожения владельца висит.
* **Бросать исключение из деструктора.** При размотке стека это завершает программу.
* **Полагаться на порядок деструкторов элементов `std::vector`.** Стандарт его не задаёт.
* **Проверять `expired()`, а затем использовать объект.** Между проверкой и использованием объект может умереть; используйте `lock()` и проверяйте результат.

<!-- section: summary -->
## Итоги

* RAII: ресурс принадлежит объекту, деструктор освобождает его на любом пути выхода, включая исключение.
* `unique_ptr` - выбор по умолчанию; владение передаётся `std::move`. `shared_ptr` - когда владельцев действительно несколько; `weak_ptr` - наблюдатель и разрыв циклов.
* Сырой указатель или ссылка в сигнатуре - «не владею». Владение выражайте типом.
* `make_unique` (C++14), `shared_ptr<T[]>` (C++17), `make_shared<T[]>` (C++20) проверены компиляцией с `-std`.

Дальше: [классы, наследование и виртуальные функции](lesson:cpp.fundamentals.classes-inheritance-virtual).
Назад: [ссылки, указатели и перемещение](lesson:cpp.fundamentals.references-pointers-move).
Сравните: [умные указатели в Rust](lesson:rust.fundamentals.smart-pointers-and-interior-mutability), [исключения и освобождение ресурсов в C#](lesson:csharp.fundamentals.exceptions-and-resources), [try-with-resources в Java](lesson:java.fundamentals.exceptions-try-with-resources).
