<!-- section: simple -->
## Что проверяет эта проверка

Атрибуты класса и экземпляра, методы и `@classmethod`, `@property`, специальные методы (`__repr__`, `__eq__`, `__lt__`, `__hash__`), `@dataclass` с `frozen` и `order`, наследование и порядок разрешения методов, композиция и протоколы (утиная типизация).

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Классы и объекты изнутри](lesson:python.advanced.classes-and-objects), [Специальные методы и dataclasses](lesson:python.advanced.dunder-methods-and-dataclasses), [Наследование, композиция и протоколы](lesson:python.advanced.inheritance-composition-protocols).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```python run
from dataclasses import dataclass

@dataclass(frozen=True, order=True)
class Point:
    x: int
    y: int = 0

    def shifted(self, dx):
        return Point(self.x + dx, self.y)

print(Point(1) < Point(2), Point(1, 5).shifted(2), {Point(1), Point(1)})
```
```output
True Point(x=3, y=5) {Point(x=1, y=0)}
```

`@dataclass` создаёт `__init__`, `__repr__`, `__eq__`; `order=True` добавляет сравнения по полям, `frozen=True` запрещает присваивание и позволяет хешировать, поэтому два равных `Point` в множестве - один элемент.

<!-- section: precise -->
## Что нужно уметь

* отличать атрибуты класса от атрибутов экземпляра;
* писать `@classmethod`, `@property`, `__repr__`, `__eq__` и знать связь `__eq__` с `__hash__`;
* использовать `@dataclass` с `frozen`, `order` и значениями по умолчанию;
* предсказывать порядок разрешения методов и работу `super()`;
* выбирать композицию или наследование и понимать утиную типизацию и протоколы.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Изменяемый атрибут класса общий для всех экземпляров.
* Определение `__eq__` без `__hash__` делает класс нехешируемым.
* `order=True` сравнивает поля в порядке объявления.
* Изменяемое значение по умолчанию в dataclass (`x: list = []`) запрещено; нужен `field(default_factory=list)`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Классы и объекты изнутри](lesson:python.advanced.classes-and-objects), [Специальные методы и dataclasses](lesson:python.advanced.dunder-methods-and-dataclasses), [Наследование, композиция и протоколы](lesson:python.advanced.inheritance-composition-protocols). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
