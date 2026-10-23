<!-- section: simple -->
## Что проверяет эта проверка

`throw`, `try / catch / finally`, `Exception` и `Error`, свои классы исключений, цепочки `getPrevious`, `match` со строгим сравнением, `match (true)` и `UnhandledMatchError`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Ошибки, исключения и выражение match](lesson:php.fundamentals.errors-and-match).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```php run
<?php
declare(strict_types=1);

final class LimitException extends RuntimeException
{
}

function check(int $n): string
{
    return match (true) {
        $n < 0 => throw new InvalidArgumentException("отрицательное: {$n}"),
        $n > 100 => throw new LimitException("слишком большое: {$n}"),
        $n === 0 => "ноль",
        default => "норма",
    };
}

foreach ([5, 0, -1, 500] as $n) {
    try {
        echo check($n), "\n";
    } catch (InvalidArgumentException | LimitException $e) {
        echo get_class($e), ": ", $e->getMessage(), "\n";
    } finally {
        echo "-\n";
    }
}
try {
    echo intdiv(1, 0);
} catch (DivisionByZeroError $e) {
    echo $e->getMessage(), "\n";
}
echo match (3) {
    1, 2 => "мало",
    3, 4 => "средне",
    default => "много",
}, "\n";
```
```output
норма
-
ноль
-
InvalidArgumentException: отрицательное: -1
-
LimitException: слишком большое: 500
-
Division by zero
средне
```

`match` сравнивает строго, возвращает значение и в форме `match (true)` проверяет условия по порядку; `throw` в PHP 8 - выражение, поэтому его можно написать прямо в ветке. Свой класс исключения наследуется от стандартного, а `catch` может перечислять несколько типов через `|`. Блок `finally` выполняется после каждой итерации, а деление на ноль в `intdiv` бросает `DivisionByZeroError`.

<!-- section: precise -->
## Что нужно уметь

* бросать и ловить исключения, перечислять несколько типов в `catch`, различать `Exception` и `Error` (`TypeError`, `DivisionByZeroError`, `ValueError`);
* создавать свои исключения наследованием и передавать причину третьим аргументом конструктора (`getPrevious`);
* предсказывать порядок `try`, `catch`, `finally` и влияние `return` в `finally`;
* использовать `match`: строгое сравнение, отсутствие «проваливания», `UnhandledMatchError` без `default`, форма `match (true)`;
* применять `throw` как выражение (`?? throw ...`, ветка `match`).

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `match` без `default` и без подходящей ветки бросает `UnhandledMatchError`; это не `null` и не предупреждение.
* `match ("1") { 1 => ... }` не срабатывает: сравнение строгое, строка и число различаются.
* `catch (Exception $e)` не поймает `TypeError` и `DivisionByZeroError`: это наследники `Error`, а не `Exception`.
* Пустой блок `catch` скрывает ошибку; при перебросе исключения причину передают третьим аргументом, чтобы не потерять её.
* Значение `return` внутри `finally` перекрывает и `return`, и исключение из `try`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Ошибки, исключения и выражение match](lesson:php.fundamentals.errors-and-match). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
