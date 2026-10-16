<!-- section: simple -->
## this - это «кто вызвал», а не «где написано»

Внутри функции ключевое слово `this` указывает на «текущий объект». Но какой это объект, определяется **не местом, где функция записана, а способом её вызова**:

* `user.greet()` - вызов как метод: `this` равно `user`;
* `greet()` - обычный вызов: в строгом режиме `this` равно `undefined`;
* `new Greeter()` - вызов с `new`: `this` - только что созданный объект;
* `greet.call(obj)`, `greet.apply(obj)`, `greet.bind(obj)` - `this` задан явно.

**Стрелочные функции** - исключение: у них нет собственного `this`, они берут его из окружающего кода в месте объявления.

```javascript run
"use strict";
const user = {
  name: "Анна",
  greet() { return "Привет, " + this.name; },
};
console.log(user.greet());
const detached = user.greet;
try {
  detached();
} catch (e) {
  console.log(e.name + ": " + e.message);
}
```
```output
Привет, Анна
TypeError: Cannot read properties of undefined (reading 'name')
```

Если достать метод из объекта и вызвать без объекта, `this` теряется. Это самая частая ошибка с `this`.

<!-- section: precise -->
## Правила this, call/apply/bind и строгий режим

### Правила определения this (по убыванию приоритета)

1. **`new F()`**: создаётся новый объект, `this` указывает на него.
2. **Явная привязка**: `f.call(thisArg, a, b)` и `f.apply(thisArg, [a, b])` вызывают функцию сразу; `f.bind(thisArg, a)` возвращает новую функцию с зафиксированным `this` и, при желании, началом списка аргументов. Привязанную функцию нельзя перепривязать.
3. **Вызов как метода**: в `obj.f()` значение `this` равно `obj` - тому, что стоит слева от точки *в момент вызова*.
4. **Обычный вызов**: `f()`. В строгом режиме `this` равно `undefined`; в нестрогом - глобальный объект.

Важно: `const g = obj.f; g()` - это уже правило 4. Так же теряется `this`, если метод передан как колбэк: `[1, 2].map(obj.f)`, `emitter.on("x", obj.f)`.

### Строгий режим

Директива `"use strict"` в начале файла или функции включает строгий режим. Автоматически он включён в **ES-модулях** и в **теле классов** ([MDN: Strict mode](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Strict_mode)). Для `this` это означает: при обычном вызове вы получите `undefined` и сразу понятную ошибку `TypeError`, а не тихую работу с глобальным объектом, как в нестрогом режиме.

### Стрелочные функции

Стрелочная функция не создаёт собственного `this` (и `arguments`, и `super`): эти имена ищутся в окружающей области, как обычные переменные. Поэтому:

* `call`, `apply`, `bind` не могут изменить её `this` (они лишь передают аргументы);
* она подходит для колбэков внутри методов - `this` метода остаётся доступным;
* она **не подходит** как метод объекта, если внутри нужен `this` самого объекта, и не может использоваться с `new` ([ECMAScript: Arrow Function Definitions](https://tc39.es/ecma262/#sec-arrow-function-definitions)).

```javascript run
const timer = {
  seconds: 0,
  ticks: [1, 2, 3],
  startArrow() {
    this.ticks.forEach((n) => { this.seconds += n; });
    return this.seconds;
  },
  startFunction() {
    "use strict";
    try {
      this.ticks.forEach(function (n) { this.seconds += n; });
    } catch (e) {
      return e.name;
    }
  },
};
console.log(timer.startArrow());
console.log(timer.startFunction());
```
```output
6
TypeError
```

### call, apply, bind

```javascript run
function describe(greeting, punct) {
  return greeting + ", " + this.name + punct;
}
const anna = { name: "Анна" };
console.log(describe.call(anna, "Привет", "!"));
console.log(describe.apply(anna, ["Здравствуй", "."]));
const hello = describe.bind(anna, "Привет");
console.log(hello("?"));
console.log(hello.call({ name: "Игорь" }, "!"));
```
```output
Привет, Анна!
Здравствуй, Анна.
Привет, Анна?
Привет, Анна!
```

Последняя строка показывает, что у привязанной функции `this` уже не изменить.

### Функции как значения

Функции - объекты первого класса: их можно хранить в переменных, передавать и возвращать. У функции есть свойства `name` и `length` (число параметров до первого с значением по умолчанию или rest). Параметры по умолчанию вычисляются при каждом вызове, `...rest` собирает остальные аргументы в настоящий массив (в отличие от псевдомассива `arguments`, которого нет у стрелочных функций).

<!-- section: examples -->
## Примеры

**Стрелка как метод - ловушка.** Стрелочная функция запоминает `this` места, где она создана, а не объекта-владельца.

```javascript run
const counter = {
  count: 5,
  regular() { return this.count; },
  arrow: () => typeof this,
};
console.log(counter.regular(), counter.arrow());
```
```output
5 object
```

Здесь `this` верхнего уровня файла CommonJS указывает на `module.exports`, то есть на объект, поэтому `typeof this` равно `"object"`, но это не `counter`. Конкретное значение `this` на верхнем уровне зависит от того, скрипт это, CommonJS или ES-модуль (в ES-модуле это `undefined`), то есть от среды, а не от языка.

**Потеря this в колбэке и три способа починить.**

```javascript run
"use strict";
class Button {
  constructor(label) {
    this.label = label;
    this.onClickBound = this.onClick.bind(this);
    this.onClickArrow = () => this.label + " (стрелка)";
  }
  onClick() { return this.label + " (метод)"; }
}
const b = new Button("ОК");
const run = (cb) => cb();
try { run(b.onClick); } catch (e) { console.log("потеряно:", e.name); }
console.log(run(b.onClickBound));
console.log(run(b.onClickArrow));
console.log(run(() => b.onClick()));
```
```output
потеряно: TypeError
ОК (метод)
ОК (стрелка)
ОК (метод)
```

**Метод класса не привязан, поле со стрелкой привязано.** Поле класса со стрелкой создаёт на каждый экземпляр свою функцию, зато `this` не теряется; метод в `prototype` общий и дешевле.

```javascript run
class Counter {
  count = 0;
  inc = () => ++this.count;
  reset() { this.count = 0; }
}
const c = new Counter();
const { inc } = c;
inc(); inc();
console.log(c.count, Object.hasOwn(c, "inc"), Object.hasOwn(c, "reset"));
```
```output
2 true false
```

<!-- section: mistakes -->
## Типичные ошибки

* **Извлечение метода.** `const { log } = logger; log("x")` и `setTimeout(obj.method, 0)` теряют `this`. Привяжите (`bind`), оберните в стрелку или используйте метод через объект.
* **Стрелочная функция как метод объекта**, которая обращается к `this.field`. Используйте сокращённый синтаксис метода `name() {}`.
* **`function` внутри метода как колбэк.** У неё свой `this`. Берите стрелку.
* **Ожидание, что `bind` можно «переделать».** Второй `bind` или `call` не меняет уже привязанный `this`.
* **Использование `this` в нестрогом режиме вслепую.** Вы тихо получите глобальный объект и запишете в него свойства. Включайте строгий режим (ES-модули и классы уже в нём).
* **Вызов стрелки с `new`.** Это `TypeError`: стрелка не конструктор.
* **Путаница языка и среды.** Какой `this` получит обработчик события в браузере, определяет DOM API, а не язык; в Node.js колбэк `setTimeout` тоже получает `this` от среды. Не опирайтесь на такие детали - используйте стрелки или `bind`.

<!-- section: summary -->
## Итоги

* `this` определяется способом вызова: `new`, явная привязка, метод, обычный вызов.
* Достав метод из объекта, вы теряете `this`; чините `bind`, стрелкой-обёрткой или полем класса со стрелкой.
* Стрелочные функции берут `this` из окружения и не подчиняются `call`/`apply`/`bind`; они не конструкторы.
* Строгий режим (ES-модули, классы) превращает тихий глобальный `this` в `undefined` и заметную ошибку.
* `call` и `apply` вызывают сразу, `bind` возвращает новую функцию.
