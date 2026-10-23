# Трек JavaScript: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталон: [go.md](go.md). Маршрут: `content/routes/javascript.yaml`.

```
javascript-from-zero (уровень 0) ──┐
                                   ├─> javascript-first-steps (как работает JS/Node, равенство) ─> javascript-fundamentals
javascript-for-programmers (1) ────┘     (React, Express, TypeScript, Node.js опираются на fundamentals)
   зачитывает уроки javascript-from-zero
javascript-skill-check: проверки компетенций
```

## Особенности JavaScript, которые определили порядок

* Программы выполняются в Node.js (`node main.js`), вывод - `console.log`. Браузер и DOM в треке не используются.
* Чтение ввода в Node (`require("node:fs").readFileSync(0, "utf8")`) - не первая тема для новичка: в нём сразу модули,
  кодировки и разбиение строки. Поэтому ввод вынесен в блок практики (урок 21), а до него упражнения проверяют либо
  точный вывод программы, либо **функции**.
* Функции в JavaScript - центральное понятие (колбэки, методы массивов), поэтому они появляются рано (урок 08), и с этого
  урока упражнения - функции, которые проверка вызывает с разными значениями (`harness`). Слово `export` перед функцией
  объясняется как «делает функцию доступной проверке»; модули подробно - в `javascript-fundamentals`.
* `==` и приведение типов не преподаются как норма: с первого урока о сравнении используется `===`, а причина объясняется.
* `let`/`const` вместо `var`; `var`, `this`, прототипы, промисы - в `javascript-fundamentals`.

## javascript-from-zero (уровень 0)

Id `javascript.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `main.js` (io) или `<name>.mjs` (harness).

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа; JavaScript и Node.js; `console.log` | js.program, js.console-log, js.string-literal | io, A |
| 02 | reading-errors | SyntaxError, ReferenceError, TypeError: как читать сообщение и номер строки | js.error-message | io, B (исправить) |
| **Блок 1. Значения** |||||
| 03 | values-and-types | number, string, boolean, `typeof` | js.number, js.string, js.boolean, js.typeof | io, B |
| 04 | variables | `let`, `const`, присваивание | js.variable, js.let, js.const, js.assignment | io, B |
| 05 | arithmetic | `+ - * / % **`, `Math.floor`, `Math.round`, неточность дробей | js.arithmetic, js.math | io, B |
| 06 | strings | склейка, `length`, шаблонные строки `${}` | js.concatenation, js.template-literal, js.string-length | io, B |
| 07 | comparison-and-logic | `=== !== < > <= >=`, `&& \|\| !`, почему не `==` | js.comparison, js.logical-operators | io, B |
| **Блок 2. Функции и управление** |||||
| 08 | functions | объявление, параметры, `return`, вызов; `export` для проверки | js.function, js.parameter, js.return, js.export-for-check | harness, B |
| 09 | if-else | `if / else if / else` | js.if | harness, C |
| 10 | while-loop | `while`, счётчик | js.while | harness, C |
| 11 | for-loop | `for (let i = ...)` | js.for | harness, C |
| 12 | loop-control | `break`, `continue`, вложенные циклы | js.break-continue, js.nested-loops | harness, C |
| **Блок 3. Коллекции** |||||
| 13 | arrays | литерал, индекс, `length`, `push`, `undefined` за границей | js.array, js.index, js.push | harness, C |
| 14 | iterating-arrays | `for...of`, обход с индексом, сумма/максимум | js.for-of | harness, C |
| 15 | arrow-functions | стрелочные функции, функция как значение и аргумент | js.arrow-function, js.callback | harness, C |
| 16 | array-methods | `map`, `filter`, `find`, `includes`, `join`, `toSorted`/`sort` с компаратором | js.array-methods | harness, C |
| 17 | string-methods | `trim`, `split`, `toUpperCase`, `slice`, `includes`, `replaceAll` | js.string-methods | harness, C |
| 18 | objects | литерал объекта, свойства, точка и скобки, добавление, `in`/`Object.keys` | js.object, js.property | harness, C |
| 19 | arrays-of-objects | массив объектов: поиск, группировка, итоги | js.object-iteration | harness, C |
| **Блок 4. Надёжность и практика** |||||
| 20 | errors-and-exceptions | `throw new Error`, `try/catch`, проверка входных данных | js.throw, js.try-catch | harness, C |
| 21 | reading-input | чтение ввода в Node: `readFileSync(0, "utf8")`, строки, `Number()`, `NaN` | js.read-input, js.number-parsing | io, C |
| 22 | scope | блоки, `let`/`const` видны в блоке, затенение | js.scope | harness, B (найти ошибку) |
| 23 | mini-project | мини-проект: бюджет из строк ввода (итоги по категориям, плохие строки) | - | io, D |

## javascript-for-programmers (уровень 1)

Id `javascript.programmers.<slug>`; упражнение - harness, квиз на ловушки JS (`==` и приведение, `NaN`, `0.1 + 0.2`,
`let`/`const`/TDZ, `undefined` vs `null`, сравнение массивов по ссылке, `sort` без компаратора сортирует строки,
`for...in` vs `for...of`).

| # | slug | assessmentFor (`javascript.from-zero.*`) |
|---|---|---|
| 01 | syntax-and-control-flow | 01-12 |
| 02 | collections-and-functions | 13-19, 22 |
| 03 | errors-and-input | 20, 21 |

## javascript-skill-check

Id `javascript.check.<slug>`; урок-проверка = упражнение (harness) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, условия, циклы, функции | basics | from-zero 01-12 |
| Массивы, объекты, методы массивов | collections | from-zero 13-19, 22 |
| Ошибки и ввод | errors-input | from-zero 20, 21 |
| Типы и приведение, равенство | coercion | javascript.first-steps.values-and-equality, javascript.fundamentals.types-and-coercion |
| Замыкания и this | closures | javascript.fundamentals.scope-and-closures, this-and-functions |
| Объекты, прототипы, классы | objects | javascript.fundamentals.objects-and-prototypes |
| Итерация и генераторы | iteration | javascript.fundamentals.arrays-and-iteration |
| Промисы и async/await | async | javascript.fundamentals.promises-and-async |
| Модули и тесты | modules | javascript.fundamentals.modules-and-testing |

## Связи

* `javascript-first-steps`: `requires: [javascript-from-zero]`; урок 01 зависит от `javascript.from-zero.reading-input`.
* `javascript-fundamentals`: `requires: [javascript-first-steps]` (раньше зависимость шла только через prerequisite урока).
