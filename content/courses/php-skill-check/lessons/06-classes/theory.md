<!-- section: simple -->
## Что проверяет эта проверка

Классы с promoted-свойствами и `readonly`, методы и видимость, `enum` с типом и методами, `from` и `tryFrom`, интерфейсы, `static` и `new static`, поведение объектов при присваивании и `clone`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первый класс](lesson:php.from-zero.classes-intro), [Классы, readonly, перечисления и первоклассные вызываемые](lesson:php.fundamentals.classes-readonly-enums), [Интерфейсы, абстрактные классы, трейты и позднее статическое связывание](lesson:php.fundamentals.interfaces-traits-lsb).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```php run
<?php
declare(strict_types=1);

enum Priority: int
{
    case Low = 1;
    case High = 3;

    public function label(): string
    {
        return match ($this) {
            self::Low => "низкий",
            self::High => "высокий",
        };
    }
}

interface Describable
{
    public function describe(): string;
}

final class Task implements Describable
{
    private bool $done = false;

    public function __construct(
        public readonly string $title,
        public readonly Priority $priority,
    ) {
    }

    public function complete(): void
    {
        $this->done = true;
    }

    public function describe(): string
    {
        return ($this->done ? "[x] " : "[ ] ") . "{$this->title} ({$this->priority->label()})";
    }
}

$task = new Task("Купить хлеб", Priority::High);
$task->complete();
echo $task->describe(), "\n";
echo Priority::tryFrom(2)?->label() ?? "нет такого", " ", Priority::from(1)->name, "\n";
try {
    $task->title = "Другое";
} catch (Error $e) {
    echo get_class($e), ": ", $e->getMessage(), "\n";
}
```
```output
[x] Купить хлеб (высокий)
нет такого Low
Error: Cannot modify readonly property Task::$title
```

Класс объединяет данные и методы. Свойство `readonly` можно задать только один раз, поэтому попытка изменить `$task->title` бросает `Error`. Перечисление с типом `int` создаётся из значения через `from` (с ошибкой для неизвестного значения) или `tryFrom` (с `null`). Интерфейс `Describable` гарантирует, что у класса есть метод `describe`.

<!-- section: precise -->
## Что нужно уметь

* объявлять класс с конструктором и promoted-свойствами (`public readonly string $title`), методами и видимостью `public`, `private`;
* понимать `readonly`: значение задаётся один раз, изменение даёт `Error`;
* объявлять `enum` с типом (`: int`, `: string`), методами и использовать `from`, `tryFrom`, `->name`, `->value`, `cases()`;
* реализовывать интерфейс и принимать его как тип параметра; различать `interface`, `abstract class` и `trait`;
* понимать `static`, `self` и позднее статическое связывание (`static::`);
* использовать `match ($this)` в методе перечисления.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Попытка изменить `readonly`-свойство даёт `Error: Cannot modify readonly property`, а не молчаливое игнорирование.
* `Priority::from(9)` бросает `ValueError`; если значение приходит из ввода, используйте `tryFrom` и проверку на `null`.
* Класс, который `implements` интерфейс, обязан реализовать все его методы, иначе возникает фатальная ошибка при загрузке.
* `new` у `abstract class` и у интерфейса невозможен; `new static` создаёт экземпляр класса, у которого вызвали метод, а `new self` - класса, где метод написан.
* Объекты передаются по «идентификатору»: присваивание `$b = $a` не копирует объект, а `clone` копирует.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первый класс](lesson:php.from-zero.classes-intro), [Классы, readonly, перечисления и первоклассные вызываемые](lesson:php.fundamentals.classes-readonly-enums), [Интерфейсы, абстрактные классы, трейты и позднее статическое связывание](lesson:php.fundamentals.interfaces-traits-lsb). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
