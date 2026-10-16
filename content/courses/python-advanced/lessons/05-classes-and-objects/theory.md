<!-- section: simple -->
## Объект - это словарь с привязанным классом

Класс в Python - не «чертёж на бумаге», а живой объект, который существует во время работы программы. Когда вы пишете `obj.name`, Python ищет `name` по понятному маршруту: сначала в самом объекте, затем в его классе, затем в базовых классах. Первое найденное значение и есть ответ.

Из этого правила следует почти всё остальное:

* У каждого экземпляра есть свой словарь атрибутов (`vars(obj)`), в который попадает всё, что вы присваиваете через `self.x = ...`.
* Атрибуты и методы класса лежат в словаре класса и **общие** для всех экземпляров. Поэтому изменяемое значение (список), положенное в тело класса, делят все объекты - классическая ловушка.
* Метод - это функция в классе. Вызов `obj.method()` Python превращает в `Class.method(obj)`: объект подставляется первым аргументом (`self`).

Дополнительные виды методов: `@classmethod` получает класс (`cls`) - так делают «альтернативные конструкторы»; `@staticmethod` не получает ничего лишнего и является просто функцией, которой удобно жить рядом с классом; `@property` позволяет писать `t.fahrenheit` как обычный атрибут, а на деле вызывать код (вычисление или проверку).

Про наследование и порядок поиска по базовым классам речь пойдёт в уроке про [наследование и композицию](lesson:python.advanced.inheritance-composition-protocols). Здесь мы разбираем один класс и его экземпляры.

<!-- section: precise -->
## Поиск атрибута и дескрипторы

**Чтение `obj.attr`.** Грубо, для обычных классов порядок такой:

1. Среди атрибутов типа (`type(obj)` и его базовых классов в порядке MRO) ищется `attr`. Если найден **дескриптор данных** (объект с `__get__` и `__set__`/`__delete__`, например `property`), используется он.
2. Иначе, если `attr` есть в `obj.__dict__`, берётся значение оттуда.
3. Иначе используется найденный в классе атрибут: если у него есть `__get__` (функция, `classmethod`, `staticmethod`), вызывается `__get__`; иначе берётся сам объект.
4. Иначе `AttributeError` (после попытки `__getattr__`, если он определён).

**Присваивание `obj.attr = v`** по умолчанию записывает значение в `obj.__dict__` (или вызывает `__set__` дескриптора данных, как у `property` с setter). Оно **никогда** не изменяет атрибут класса. Поэтому `self.count += 1` - это `self.count = self.count + 1`: чтение берёт значение класса, запись создаёт атрибут экземпляра.

**Методы как дескрипторы.** Функция в теле класса имеет метод `__get__`. При `obj.method` вызывается `function.__get__(obj, type(obj))` и возвращается *связанный метод* (bound method) с атрибутами `__self__` (объект) и `__func__` (исходная функция). Обращение `Class.method` даёт обычную функцию (в Python 3 нет «несвязанных методов»).

**classmethod и staticmethod.** `@classmethod` связывает первым аргументом класс, через который метод вызван: `Sub.from_x(...)` получает `cls=Sub`. Поэтому внутри нужно писать `cls(...)`, а не жёстко имя базового класса - тогда подкласс получит экземпляр своего типа. `@staticmethod` ничего не связывает; используйте его, когда функции не нужны ни `self`, ни `cls`, но она логически относится к классу.

**property.** `@property` превращает метод в атрибут только для чтения; `@имя.setter` добавляет запись, `@имя.deleter` - удаление. Свойства используют для вычисляемых значений (`fahrenheit` из `celsius`) и для проверки при присваивании. Само значение хранят в «приватном» атрибуте (`_celsius`) - иначе setter будет бесконечно вызывать сам себя. Соглашение: имя с одним подчёркиванием означает «внутреннее», это не защита.

**Атрибуты класса и экземпляра.** Атрибут класса удобен для констант и общих счётчиков (обращайтесь к нему через класс: `Cls.counter += 1`). Изменяемые значения (списки, словари) нужно создавать в `__init__`, иначе они общие. 

**`__slots__`.** `__slots__ = ("x", "y")` в теле класса заменяет словарь экземпляра фиксированными слотами: экземпляры занимают меньше памяти, присвоить неописанный атрибут нельзя (`AttributeError`), `vars(obj)` недоступен. Ограничения: нельзя задать значение по умолчанию для имени слота тем же именем в классе, и при наследовании подкласс без своего `__slots__` снова получает `__dict__`. Применяйте, когда создаются миллионы небольших объектов.

<!-- section: examples -->
## Примеры

**Пример 1. Поиск атрибута: экземпляр, затем класс.**

```python run
class Dog:
    species = "dog"

    def __init__(self, name):
        self.name = name

rex = Dog("Rex")
print(rex.species, vars(rex))
rex.species = "wolf"
print(rex.species, Dog.species, vars(rex))
del rex.species
print(rex.species)
print("name" in Dog.__dict__, "species" in Dog.__dict__)
```
```output
dog {'name': 'Rex'}
wolf dog {'name': 'Rex', 'species': 'wolf'}
dog
False True
```

**Пример 2. Связанные методы, classmethod и staticmethod.**

```python run
class Shape:
    def area(self):
        return 0

    @classmethod
    def unit(cls):
        return cls()

    @staticmethod
    def describe(n):
        return f"фигур: {n}"

class Square(Shape):
    pass

s = Square()
m = s.area
print(type(m).__name__, m.__self__ is s, m.__func__ is Shape.__dict__["area"])
print(Shape.area(s) == s.area())
print(type(Square.unit()).__name__, type(Shape.unit()).__name__)
print(Shape.describe(3), s.describe(4))
```
```output
method True True
True
Square Shape
фигур: 3 фигур: 4
```

**Пример 3. property с проверкой и вычисляемым значением.**

```python run
class Circle:
    def __init__(self, radius):
        self.radius = radius

    @property
    def radius(self):
        return self._radius

    @radius.setter
    def radius(self, value):
        if value < 0:
            raise ValueError("радиус не может быть отрицательным")
        self._radius = value

    @property
    def area(self):
        return 3.14159 * self._radius ** 2

c = Circle(2)
print(c.radius, round(c.area, 2))
c.radius = 3
print(round(c.area, 2))
try:
    c.radius = -1
except ValueError as exc:
    print("ValueError:", exc)
try:
    c.area = 5
except AttributeError:
    print("area нельзя присвоить: нет setter")
```
```output
2 12.57
28.27
ValueError: радиус не может быть отрицательным
area нельзя присвоить: нет setter
```

**Пример 4. Ловушки изменяемого атрибута класса и += ; __slots__.**

```python run
class Basket:
    items = []
    count = 0

    def add(self, x):
        self.items.append(x)
        self.count += 1

a, b = Basket(), Basket()
a.add("яблоко")
b.add("груша")
print(a.items, b.items, a.items is b.items)
print(Basket.count, a.count, b.count)

class P:
    __slots__ = ("x", "y")
    def __init__(self, x, y):
        self.x, self.y = x, y

p = P(1, 2)
print(hasattr(p, "__dict__"))
try:
    p.z = 3
except AttributeError as exc:
    print(type(exc).__name__)
```
```output
['яблоко', 'груша'] ['яблоко', 'груша'] True
0 1 1
False
AttributeError
```

<!-- section: mistakes -->
## Типичные ошибки

* **Изменяемый атрибут в теле класса** (`items = []`) - один список на все экземпляры. Создавайте его в `__init__`.
* **`self.count += 1` для общего счётчика.** Создаёт атрибут экземпляра; общий счётчик меняйте через класс (`Cls.count += 1`).
* **Забытый `self`.** `def area():` внутри класса даёт `TypeError` при `obj.area()` - объект подставляется первым аргументом.
* **Жёсткое имя класса в `@classmethod`.** `return Temperature(...)` не учитывает подклассы; пишите `cls(...)`.
* **Setter, пишущий в то же имя.** `self.radius = value` внутри setter для `radius` вызовет его же и приведёт к `RecursionError`; храните значение в `_radius`.
* **Присваивание свойству без setter** - `AttributeError`. Это не баг, а защита вычисляемых значений.
* **`@staticmethod` там, где нужен `cls`.** Метод не узнает, из какого класса его вызвали.
* **Ожидание, что `__slots__` уберёт `__dict__` при наследовании.** Без своего `__slots__` в подклассе словарь вернётся.

<!-- section: summary -->
## Итог

* `obj.attr`: дескриптор данных в типе, затем `__dict__` экземпляра, затем класс и базовые классы; присваивание пишет в экземпляр.
* Метод - функция-дескриптор; `obj.method` создаёт связанный метод (`__self__`, `__func__`).
* `@classmethod` получает `cls` (используйте для альтернативных конструкторов), `@staticmethod` - обычная функция в пространстве имён класса, `@property` - атрибут, вычисляемый кодом.
* Изменяемые данные экземпляра создавайте в `__init__`; общие данные меняйте через класс.
* `__slots__` экономит память и запрещает произвольные атрибуты.
* Дальше: [специальные методы и dataclasses](lesson:python.advanced.dunder-methods-and-dataclasses) и [наследование, композиция и протоколы](lesson:python.advanced.inheritance-composition-protocols).
