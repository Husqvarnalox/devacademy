<!-- section: simple -->
## Модель - это контракт с проверкой

В предыдущем уроке мы описывали тело запроса классом, наследующим `BaseModel`. Теперь разберём этот класс подробнее. **Pydantic** - библиотека, которая берёт «грязные» данные из внешнего мира (JSON, строка запроса, словарь) и гарантирует: если объект модели создан, то все его поля имеют нужные типы и удовлетворяют ограничениям. Иначе будет `ValidationError` со списком всех проблем сразу.

Это не то же самое, что аннотации типов в обычном Python. Аннотация `x: int` для интерпретатора - просто подсказка; `mypy` проверит её до запуска, но во время работы туда может попасть что угодно. В модели Pydantic аннотация **исполняется**: `"5"` станет `5`, а `"abc"` вызовет ошибку.

Принцип: на границе системы (HTTP-запрос, файл, очередь) данные недоверенные - их превращают в модель. Внутри системы вы уже работаете с проверенными объектами и не повторяете проверки.

Модели решают три задачи FastAPI одновременно: проверяют вход (`422` при ошибке), фильтруют выход (`response_model`) и описывают формат в OpenAPI. Версия, которую использует курс, - **Pydantic v2**; старые имена v1 (`.dict()`, `@validator`, `class Config`) в новом коде не используйте.

Пользоваться обычными [dataclass](lesson:python.advanced.dunder-methods-and-dataclasses) вместо моделей можно, но они не проверяют данные при создании: для границ системы нужен Pydantic.

<!-- section: precise -->
## Как это устроено

**Определение и проверка.** При определении класса Pydantic по аннотациям строит схему валидации и компилирует её в быстрый валидатор в `pydantic-core` (написан на Rust). Создание экземпляра `Model(**data)` или `Model.model_validate(data)` запускает этот валидатор. Режим по умолчанию - `lax`: допустимые приведения выполняются (`"5"` -> `5`, `"2025-03-01"` -> `date`), недопустимые дают ошибку. Режим `strict` (`ConfigDict(strict=True)` или `Field(strict=True)`) приведение запрещает.

**Поля и ограничения.** Значение по умолчанию делает поле необязательным; `Field(...)` добавляет ограничения: `gt/ge/lt/le`, `min_length/max_length`, `pattern`, `decimal_places`, `max_digits`, а также `alias`, `description`. Тип `X | None` без значения по умолчанию - поле **обязательное**, но допускающее `None` (отличие от Pydantic v1). Для повторяемых ограничений используют `Annotated[str, StringConstraints(...)]` и дают такому типу имя.

**Порядок преобразований.** `StringConstraints(strip_whitespace=True, to_upper=True, pattern=...)` задаёт и проверки, и преобразования. В проверенной версии `pattern` применяется к значению **до** `to_upper`, поэтому `" ab-1 "` с шаблоном на заглавные буквы не пройдёт. Если порядок важен, расположите явные `BeforeValidator`/`AfterValidator` в `Annotated` перед ограничениями или используйте `@field_validator(..., mode="before")`.

**Конфигурация.** `model_config = ConfigDict(...)`: `extra` (`"ignore"` по умолчанию, `"forbid"` - ошибка `extra_forbidden`, `"allow"`), `str_strip_whitespace`, `frozen` (неизменяемый экземпляр), `strict`, `populate_by_name`, `alias_generator`, `from_attributes` (создание из атрибутов объекта, ORM-режим).

**Валидаторы.**

* `@field_validator("a", "b")` над `@classmethod`: проверка/преобразование одного поля. `mode="after"` (по умолчанию) получает уже приведённое значение, `mode="before"` - сырое.
* `@model_validator(mode="after")` - метод экземпляра, который видит все поля (например, «конец позже начала»). `mode="before"` получает сырые данные.
* Валидатор сигнализирует об ошибке исключением `ValueError` (или `AssertionError`); Pydantic превращает его в ошибку с `type="value_error"`. Валидатор обязан **вернуть** значение (или `self`).

**Изменяемые значения по умолчанию.** В обычной функции `def f(x=[])` список создаётся один раз и разделяется между вызовами; стандартный `@dataclass` вообще запрещает `list` как значение по умолчанию (`ValueError`). В модели Pydantic `tags: list[str] = []` **безопасно**: для каждого экземпляра значение копируется. Тем не менее `Field(default_factory=list)` предпочтителен: он явно выражает «новый список» и работает для любых типов, а привычка не зависит от того, в каком классе вы пишете.

**Сериализация.** `model_dump()` возвращает словарь с Python-объектами; `model_dump(mode="json")` - только JSON-совместимые значения (`Decimal` -> строка, `date` -> ISO-строка); `model_dump_json()` - строку JSON. Параметры `include/exclude`, `exclude_none`, `exclude_unset`, `exclude_defaults`, `by_alias` управляют составом. `@computed_field` над `@property` добавляет вычисляемое значение в дамп и в схему. `model_copy(update=...)` создаёт копию (без повторной валидации!). Разбор: `model_validate(obj)`, `model_validate_json(text)`.

**Ошибки.** `ValidationError.errors()` возвращает список словарей `{"type", "loc", "msg", "input", "ctx"}`. Для вложенных моделей и списков `loc` - кортеж: `("items", 0, "qty")`. Ошибка модельного валидатора имеет `loc == ()`. Ошибки собираются все сразу. `FastAPI` превращает их в тело `422` (см. [предыдущий урок](lesson:fastapi.fundamentals.path-query-body)).

**Типичные расширения.** `SecretStr` скрывает значение в `repr` и JSON (но это не шифрование); `EmailStr` требует пакета `email-validator`, которого в окружении курса нет, поэтому в упражнениях используется `pattern`. Для разных форм ответа применяют объединения (`Union`) и дискриминатор (`Field(discriminator=...)`) - тема выходит за рамки курса.

**Когда не использовать.** Для внутренних структур, которые не пересекают границу системы, Pydantic избыточен: хватит `dataclass` или `NamedTuple`. Валидация больших объёмов данных в горячем цикле всё равно стоит времени; не создавайте модели ради каждой промежуточной записи.

**Тестирование.** Проверяйте модели отдельно от HTTP: `Model.model_validate(data)` и `pytest.raises(ValidationError)`, затем `exc.errors()` на нужные `loc` и `type`. Тестируйте границы: на каждое ограничение - значение на границе и сразу за ней.

**Продакшен.** Запрещайте лишние поля во внешних контрактах (`extra="forbid"`), если важно заметить опечатки клиентов; для публичных API, где клиенты добавляют поля вперёд, наоборот, полезна терпимость (`ignore`). Для денег используйте `Decimal` с `decimal_places`, а не `float`.

<!-- section: examples -->
## Примеры

**Пример 1. Приведение, ограничения и ошибки.**

```python run
from pydantic import BaseModel, Field, ValidationError

class Product(BaseModel):
    name: str = Field(min_length=2)
    price: float = Field(gt=0)
    qty: int = 1

p = Product(name="Tea", price="3.5", qty="4")
print(p, type(p.qty).__name__)
try:
    Product(name="x", price=-1, qty="many")
except ValidationError as e:
    print(e.error_count(), "ошибки")
    for err in e.errors():
        print(err["loc"], err["type"])
```
```output
name='Tea' price=3.5 qty=4 int
3 ошибки
('name',) string_too_short
('price',) greater_than
('qty',) int_parsing
```

**Пример 2. Изменяемые значения по умолчанию: модель, функция, dataclass.**

```python run
from dataclasses import dataclass
from pydantic import BaseModel

class Cart(BaseModel):
    items: list[str] = []

a, b = Cart(), Cart()
a.items.append("apple")
print("модель:", a.items, b.items)

def add(x, bucket=[]):
    bucket.append(x)
    return bucket

add(1)
print("функция:", add(2))

try:
    @dataclass
    class D:
        xs: list = []
except ValueError as exc:
    print("dataclass:", exc)
```
```output
модель: ['apple'] []
функция: [1, 2]
dataclass: mutable default <class 'list'> for field xs is not allowed: use default_factory
```

**Пример 3. Валидаторы, вычисляемое поле и сериализация.**

```python run
from decimal import Decimal
from pydantic import BaseModel, ConfigDict, Field, ValidationError, computed_field, field_validator, model_validator

class Line(BaseModel):
    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)
    name: str
    qty: int = Field(ge=1)
    price: Decimal = Field(gt=0, decimal_places=2)

    @field_validator("name")
    @classmethod
    def title(cls, v):
        return v.title()

class Invoice(BaseModel):
    lines: list[Line]
    discount: Decimal = Decimal("0")

    @model_validator(mode="after")
    def discount_not_above_total(self):
        if self.discount > self.subtotal:
            raise ValueError("discount is larger than subtotal")
        return self

    @computed_field
    @property
    def subtotal(self) -> Decimal:
        return sum((l.qty * l.price for l in self.lines), Decimal(0))

inv = Invoice(lines=[{"name": "  green tea ", "qty": 2, "price": "3.20"}], discount="1")
print(inv.model_dump())
print(inv.model_dump(mode="json"))
print(inv.model_dump_json(exclude={"discount"}))
for bad in ({"lines": [{"name": "x", "qty": 0, "price": "1.234", "color": "red"}]},
            {"lines": [{"name": "x", "qty": 1, "price": "1"}], "discount": "5"}):
    try:
        Invoice.model_validate(bad)
    except ValidationError as e:
        print([(".".join(map(str, x["loc"])), x["type"]) for x in e.errors()])
```
```output
{'lines': [{'name': 'Green Tea', 'qty': 2, 'price': Decimal('3.20')}], 'discount': Decimal('1'), 'subtotal': Decimal('6.40')}
{'lines': [{'name': 'Green Tea', 'qty': 2, 'price': '3.20'}], 'discount': '1', 'subtotal': '6.40'}
{"lines":[{"name":"Green Tea","qty":2,"price":"3.20"}],"subtotal":"6.40"}
[('lines.0.qty', 'greater_than_equal'), ('lines.0.price', 'decimal_max_places'), ('lines.0.color', 'extra_forbidden')]
[('', 'value_error')]
```

**Пример 4. Устаревшее API v1 в Pydantic v2.** Старые имена работают, но предупреждают. В новом коде пишите `model_dump()` и `model_validate()`.

```python run
import warnings
from pydantic import BaseModel

class U(BaseModel):
    name: str

u = U.model_validate({"name": "ann"})
with warnings.catch_warnings(record=True) as caught:
    warnings.simplefilter("always")
    old = u.dict()
print(old == u.model_dump(), [w.category.__name__ for w in caught])
```
```output
True ['PydanticDeprecatedSince20']
```

<!-- section: mistakes -->
## Типичные ошибки

* **Pydantic v1 в новом коде.** `class Config:`, `@validator`, `.dict()`, `parse_obj`. В v2 они устарели; используйте `model_config`, `@field_validator`, `model_dump`, `model_validate`.
* **Валидатор не возвращает значение.** Без `return v` поле станет `None`.
* **`@field_validator` без `@classmethod`.** Порядок декораторов: сначала `@field_validator`, под ним `@classmethod`.
* **Порядок преобразований и шаблона.** `to_upper=True` вместе с `pattern` не даёт того, что кажется; явные before-валидаторы делают порядок очевидным.
* **Расчёт на то, что `X | None` делает поле необязательным.** В v2 поле без значения по умолчанию обязательно, даже если допускает `None`. Пишите `= None`, если поле можно не передавать.
* **`model_copy(update=...)` без проверки.** Копия с обновлёнными значениями не проходит валидацию: используйте `model_validate` на объединённых данных, если значения приходят извне.
* **Деньги в `float`.** `0.1 + 0.2 != 0.3`; используйте `Decimal` и ограничьте `decimal_places`.
* **Доверие к `extra="ignore"` во входных контрактах.** Опечатка в имени необязательного поля молча игнорируется; `forbid` её обнаружит.
* **Ловушка изменяемых значений по умолчанию.** В моделях Pydantic список копируется, но привычка `def f(x=[])` в функциях и `list` в dataclass остаётся ошибкой.

<!-- section: summary -->
## Итоги

* Модель Pydantic парсит и проверяет данные при создании; аннотации в ней исполняются.
* Ограничения - `Field`/`Annotated`; настройки - `model_config = ConfigDict(...)`; проверки - `@field_validator` и `@model_validator`.
* `model_dump`/`model_dump_json`/`mode="json"`/`by_alias`/`exclude_*` управляют сериализацией; `@computed_field` добавляет вычисляемые поля.
* Ошибки - список `type/loc/msg`; для вложенных данных `loc` указывает точный путь.
* Изменяемые значения по умолчанию в моделях копируются, но `default_factory` остаётся лучшей практикой; в v2 не используйте API v1.
