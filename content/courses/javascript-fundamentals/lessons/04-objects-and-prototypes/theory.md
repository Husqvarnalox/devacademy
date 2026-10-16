<!-- section: simple -->
## Объект - это набор свойств и ссылка на «родителя»

Объект в JavaScript - коллекция свойств «ключ - значение», у каждого из которых есть дополнительные флаги. Кроме того, у почти каждого объекта есть скрытая ссылка на другой объект - **прототип**. Если свойство не найдено в самом объекте, поиск продолжается в прототипе, затем в прототипе прототипа и так до `null`. Эта цепочка называется **цепочкой прототипов**.

```javascript run
const animal = { eats: true, describe() { return `ест: ${this.eats}, имя: ${this.name}`; } };
const dog = Object.create(animal);
dog.name = "Рекс";
console.log(dog.describe());
console.log(Object.keys(dog), "eats" in dog, Object.hasOwn(dog, "eats"));
console.log(Object.getPrototypeOf(dog) === animal);
```
```output
ест: true, имя: Рекс
[ 'name' ] true false
true
```

Ключевое слово `class` не вводит классы в смысле Java или C#: это удобный синтаксис над той же схемой «функция-конструктор плюс прототип», с несколькими важными добавками (поля, приватные поля `#x`, `static`).

<!-- section: precise -->
## Дескрипторы, прототипы, классы и копирование

### Дескрипторы свойств

Каждое свойство имеет **дескриптор**. У свойства-данных это `value`, `writable`, `enumerable`, `configurable`. У свойства-аксессора вместо `value` и `writable` - функции `get` и `set`. Читают дескриптор через `Object.getOwnPropertyDescriptor`, задают - `Object.defineProperty`.

* `writable: false` - значение нельзя изменить присваиванием (в строгом режиме попытка даёт `TypeError`, в нестрогом молча игнорируется);
* `enumerable: false` - свойство не видно в `for...in`, `Object.keys`, спрэде и `JSON.stringify`;
* `configurable: false` - свойство нельзя удалить и нельзя перенастроить дескриптор.

Свойства, созданные обычным присваиванием или литералом, получают все три флага `true`. `Object.freeze` делает все свойства неизменяемыми, но только на первом уровне.

```javascript run
"use strict";
const account = { owner: "Анна" };
Object.defineProperty(account, "id", { value: 42, enumerable: true });
console.log(Object.getOwnPropertyDescriptor(account, "id"));
try {
  account.id = 43;
} catch (e) {
  console.log(e.name);
}
console.log(Object.getOwnPropertyDescriptor(account, "owner"));
```
```output
{ value: 42, writable: false, enumerable: true, configurable: false }
TypeError
{ value: 'Анна', writable: true, enumerable: true, configurable: true }
```

Обратите внимание: у `id` флаги `writable` и `configurable` по умолчанию `false`, потому что при `defineProperty` отсутствующие флаги не берутся из «обычных» значений.

### Цепочка прототипов

У объекта есть внутренний слот `[[Prototype]]`. Читают его через `Object.getPrototypeOf(obj)`, задают при создании через `Object.create(proto)` или, неудачно, через `Object.setPrototypeOf`. Устаревшее свойство `__proto__` - всего лишь аксессор к тому же слоту ([MDN: Object.prototype.__proto__](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Object/proto)).

* **Чтение** `obj.x` идёт по цепочке: сначала собственные свойства, затем прототип и так далее до `null`.
* **Запись** `obj.x = v` создаёт (или изменяет) **собственное** свойство и этим «затеняет» унаследованное. Исключения: на пути есть неизменяемое свойство или сеттер, тогда сработает он.
* `Object.create(null)` создаёт объект без прототипа: у него нет даже `toString`.
* Функции, вызванные с `new`, создают объект, прототипом которого становится `F.prototype`. Сравните: `F.prototype` - объект, который станет прототипом экземпляров; `Object.getPrototypeOf(F)` - прототип самой функции.

### class как синтаксис над прототипами

`class` создаёт функцию-конструктор и кладёт методы в её `prototype`. Что класс добавляет сверх «ручной» схемы ([MDN: Classes](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Classes)):

* тело класса всегда выполняется в строгом режиме;
* класс нельзя вызвать без `new`, и он не поднимается как функция (до объявления действует TDZ);
* методы неперечисляемые;
* **поля** экземпляра: `count = 0;` создаёт свойство на каждом экземпляре;
* **приватные поля и методы** `#secret`: доступны только внутри тела класса; это не свойства, их не видно через `Object.keys` и рефлексию, а обращение снаружи - синтаксическая ошибка;
* **`static`**-члены принадлежат самому классу, а не экземплярам;
* `extends` связывает две цепочки: `Child.prototype` -> `Parent.prototype` и `Child` -> `Parent` (статические члены наследуются).

### Геттеры и сеттеры

Ключевые слова `get` и `set` создают свойство-аксессор: для вызывающего кода это обычное чтение и запись, но на деле исполняются функции. Через них проверяют входные значения и вычисляют производные величины. Аксессоры класса лежат в `prototype`, а не в экземпляре.

```javascript run
class Temperature {
  #celsius;
  constructor(celsius) { this.celsius = celsius; }
  get celsius() { return this.#celsius; }
  set celsius(v) {
    if (v < -273.15) throw new RangeError("ниже абсолютного нуля");
    this.#celsius = v;
  }
  get fahrenheit() { return this.#celsius * 9 / 5 + 32; }
  static fromFahrenheit(f) { return new Temperature((f - 32) * 5 / 9); }
}
const t = new Temperature(100);
console.log(t.celsius, t.fahrenheit, Temperature.fromFahrenheit(32).celsius);
console.log(Object.keys(t), Object.getOwnPropertyNames(t), JSON.stringify(t));
```
```output
100 212 0
[] [] {}
```

### Копирование: поверхностное и глубокое

Спрэд `{ ...obj }`, `Object.assign({}, obj)` и `[...arr]` копируют **собственные перечисляемые** свойства на один уровень: вложенные объекты остаются общими. Для глубокой копии есть `structuredClone`: он копирует вложенные объекты, массивы, `Date`, `Map`, `Set`, `RegExp`, обрабатывает циклические ссылки, но бросает `DataCloneError` для функций и теряет прототип (экземпляр класса становится простым объектом). `structuredClone` - API среды выполнения (HTML-стандарт; доступен в браузерах и в Node.js), а не часть ECMAScript ([MDN: structuredClone](https://developer.mozilla.org/en-US/docs/Web/API/Window/structuredClone)).

<!-- section: examples -->
## Примеры

**Класс и «ручная» схема - одно и то же.**

```javascript run
function PointOld(x) { this.x = x; }
PointOld.prototype.double = function () { return this.x * 2; };

class Point {
  constructor(x) { this.x = x; }
  double() { return this.x * 2; }
}

const a = new PointOld(2);
const b = new Point(2);
console.log(a.double(), b.double());
console.log(typeof Point, Object.getPrototypeOf(b) === Point.prototype);
console.log(Object.keys(Point.prototype), Object.keys(PointOld.prototype));
```
```output
4 4
function true
[] [ 'double' ]
```

Методы класса неперечисляемы, а присвоенный в `PointOld.prototype` метод перечисляем.

**Затенение при записи.**

```javascript run
const proto = { level: 1 };
const child = Object.create(proto);
child.level++;
console.log(child.level, proto.level, Object.hasOwn(child, "level"));
```
```output
2 1 true
```

`child.level++` прочитал унаследованное значение, а записал новое **собственное** свойство `child.level`.

**Поверхностная и глубокая копия.**

```javascript run
const original = { name: "A", tags: ["x"], when: new Date(0) };
const shallow = { ...original };
const deep = structuredClone(original);
shallow.tags.push("y");
console.log(original.tags, deep.tags, deep.when instanceof Date);

class User { constructor() { this.n = 1; } hi() { return "hi"; } }
const clone = structuredClone(new User());
console.log(clone instanceof User, typeof clone.hi);
```
```output
[ 'x', 'y' ] [ 'x' ] true
false undefined
```

**Наследование и вызов родителя.**

```javascript run
class Shape {
  constructor(name) { this.name = name; }
  area() { return 0; }
  toString() { return `${this.name}: ${this.area()}`; }
}
class Square extends Shape {
  constructor(side) { super("квадрат"); this.side = side; }
  area() { return this.side ** 2; }
}
console.log(String(new Square(3)));
console.log(Object.getPrototypeOf(Square) === Shape, new Square(1) instanceof Shape);
```
```output
квадрат: 9
true true
```

<!-- section: mistakes -->
## Типичные ошибки

* **Путаница `prototype` и `__proto__`.** У экземпляра нет свойства `prototype`; оно есть у функций-конструкторов. Правильный способ чтения - `Object.getPrototypeOf`.
* **Изменяемое значение в прототипе.** Если положить массив в `Constructor.prototype.items`, он будет общим для всех экземпляров. Состояние создают в конструкторе.
* **Ожидание, что `{ ...obj }` копирует глубоко.** Вложенные объекты общие. Также спрэд не копирует неперечисляемые свойства и прототип.
* **Использование `structuredClone` для экземпляров классов и функций.** Прототип пропадёт, функции вызовут ошибку.
* **Приватность через соглашение `_name`.** Это обычное публичное свойство. Для настоящей приватности служит `#name`.
* **Забытый `super()` в конструкторе наследника.** До вызова `super()` обращаться к `this` нельзя: `ReferenceError`.
* **Объект как словарь с ключами пользователя.** Ключи `"__proto__"` и `"toString"` приводят к странностям. Берите `Map` или `Object.create(null)`.
* **Изменение прототипов встроенных типов** (`Array.prototype.x = ...`) ломает чужой код.

<!-- section: summary -->
## Итоги

* Свойства имеют дескрипторы (`writable`, `enumerable`, `configurable`, либо `get`/`set`); `defineProperty` задаёт их явно.
* Чтение идёт по цепочке прототипов, запись создаёт собственное свойство; `Object.create(proto)` задаёт прототип напрямую.
* `class` - синтаксис над прототипами с добавками: строгий режим, поля, `#private`, `static`, `extends`.
* Геттеры и сеттеры скрывают вычисления и проверки за обычным доступом к свойству.
* Спрэд копирует поверхностно; `structuredClone` делает глубокую копию данных, но не функций и не прототипов.
