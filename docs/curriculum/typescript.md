# Трек TypeScript: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Образцы: [javascript.md](javascript.md), [go.md](go.md). Маршрут: `content/routes/typescript.yaml`.

```
typescript-from-zero (уровень 0) ──┐
                                   ├─> typescript-first-steps (tsc и стирание типов, структурная типизация) ─> typescript-fundamentals
typescript-for-programmers (1) ────┘     (fundamentals дополнительно требует javascript-fundamentals)
   зачитывает уроки typescript-from-zero
typescript-skill-check: проверки компетенций
```

## Особенности TypeScript, которые определили порядок

* **Курс самостоятельный.** `typescript-from-zero` не требует знания JavaScript: он учит программированию через TypeScript. Главное
  педагогическое преимущество - типы записываются с первого же урока с переменными, а проверка типов объясняет ошибку до запуска
  (см. «Как программа выполняется» ниже). Порядок тем близок к `javascript-from-zero`, но все тексты написаны заново.
* **Как приложение запускает TypeScript.** Для упражнения вида `io` приложение сначала запускает `tsc --noEmit --strict` над всеми
  файлами `.ts` задания (если установлены типы Node.js: Настройки - Среды выполнения), а затем выполняет файл командой `node main.ts`
  (Node.js «срезает» аннотации типов и не проверяет их). Поэтому в уроке 01 честно сказано: код сначала **проверяется**, потом
  **выполняется**; если проверка нашла ошибки, программа не запускается, а ошибки показаны с номерами вида `TS2322`.
  Урок 02 показывает настоящие сообщения `tsc` (`TS2552`, `TS2551`, `TS1005`, `TS1002`), урок 05 - `TS2322`, уроки 06 и 08 -
  `TS2362` и `TS2367`; все сообщения получены запуском настоящей цепочки `tsc` + `node`.
* **Режим `strict` всегда включён.** В курсе используются только `let`/`const` и `===`; `var`, `==` и тип
  `any` не вводятся вообще (появится, как предупреждение, в `typescript-fundamentals`). Параметры функций и возвращаемые значения
  аннотируются всегда; переменные - аннотируются в первых уроках (чтобы тип был виден), позже, когда тип очевиден из значения,
  аннотация опускается, и урок 05 объясняет, когда её писать.
* **Целевая версия `es2022`.** В типах доступны `at`, `replaceAll`, `Array.prototype.includes`, но не `toSorted`/`toReversed`
  (ES2023) и не `Object.groupBy`: уроки используют `[...arr].sort(...)` для сортировки копии.
* **Ввод вынесен в урок 23.** В Node.js чтение stdin - это `import { readFileSync } from "node:fs"` и `readFileSync(0, "utf8")`: импорт
  модуля, номер канала, кодировка и разбор строки сразу. Поэтому, как и в JavaScript, до урока 23 упражнения проверяют либо точный
  вывод программы (stdin пуст), либо **функции**. Тот же импорт в TypeScript ещё и требует типов Node.js, что лишний довод
  не начинать с него.
* **Функции - рано (урок 09).** С этого урока упражнения - функции, которые проверка вызывает с разными значениями (`harness`:
  файл `check.mjs` загружает файл ученика `*.ts` и сравнивает результаты). Если типы нарушены, `tsc` останавливает запуск ещё до проверки,
  и ученик видит настоящую ошибку типов. Слово `export` перед функцией объясняется как «делает функцию доступной проверке»; модули
  подробно - в `typescript-first-steps`/`typescript-fundamentals`.
* **Объединения и сужение - ровно в той мере, что нужна.** `number | undefined` появляется там, где этого требует язык: результат `find`,
  чтение элемента массива через `at`. Урок 18 вводит объединение, `undefined`, сужение проверкой `=== undefined` и `??`.
  Остальное (литеральные типы, различающие объединения, `never`) - в `typescript-first-steps`/`typescript-fundamentals`.
* **Объекты - через `interface`.** Урок 20 объясняет форму объекта и называет её `interface`; это мост к `typescript-first-steps`
  (структурная типизация). `type`-псевдонимы, обобщения, классы глубже и модули - в `typescript-first-steps`/`typescript-fundamentals`.
* **`catch (error)` - всегда `unknown`** (режим `strict`), поэтому урок про исключения (22) объясняет проверку `error instanceof Error`.

## typescript-from-zero (уровень 0)

Id `typescript.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `main.ts` (io) или `<name>.ts` (harness). Понятия - `ts.*`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа; TypeScript, проверка и запуск; `console.log` | ts.program, ts.console-log, ts.string-literal | io, A |
| 02 | reading-errors | сообщения `tsc` (`TS2552`, `TS2551`, `TS1005`, `TS1002`): файл, строка, код, причина | ts.error-message | io, B (исправить) |
| **Блок 1. Значения и типы** |||||
| 03 | values-and-types | `number`, `string`, `boolean`, `typeof` | ts.number, ts.string, ts.boolean, ts.typeof | io, B |
| 04 | variables | `let`, `const`, присваивание, аннотация `: number` | ts.variable, ts.let, ts.const, ts.assignment, ts.type-annotation | io, B |
| 05 | type-checking | проверка типов: `TS2322`, вывод типа, когда аннотация нужна | ts.type-inference, ts.type-error | io, B (исправить) |
| 06 | arithmetic | `+ - * / % **`, `Math.floor`, `Math.round`, неточность дробей, `TS2362` | ts.arithmetic, ts.math | io, B |
| 07 | strings | склейка, `length`, шаблонные строки `${}` | ts.concatenation, ts.template-literal, ts.string-length | io, B |
| 08 | comparison-and-logic | `=== !== < > <= >=`, `&& \|\| !`, `TS2367` | ts.comparison, ts.logical-operators | io, B |
| **Блок 2. Функции и управление** |||||
| 09 | functions | объявление, типизированные параметры, `return`, тип результата, `export` для проверки | ts.function, ts.parameter, ts.return, ts.return-type, ts.export-for-check | harness, B |
| 10 | if-else | `if / else if / else` | ts.if | harness, C |
| 11 | while-loop | `while`, счётчик, накопление | ts.while | harness, C |
| 12 | for-loop | `for (let i = ...)` | ts.for | harness, C |
| 13 | loop-control | `break`, `continue`, вложенные циклы | ts.break-continue, ts.nested-loops | harness, C |
| **Блок 3. Коллекции** |||||
| 14 | arrays | литерал, тип `number[]`, индекс, `length`, `push`, выход за границы | ts.array, ts.array-type, ts.index, ts.push | harness, C |
| 15 | iterating-arrays | `for...of`, обход с индексом, сумма/максимум | ts.for-of | harness, C |
| 16 | arrow-functions | стрелочные функции, тип функции, функция как аргумент | ts.arrow-function, ts.function-type, ts.callback | harness, C |
| 17 | array-methods | `map`, `filter`, `find`, `includes`, `join`, сортировка копии с компаратором | ts.array-methods | harness, C |
| 18 | optional-values | объединение `T \| undefined`, сужение проверкой, `??` | ts.union, ts.undefined, ts.narrowing, ts.nullish-coalescing | harness, C |
| 19 | string-methods | `trim`, `split`, `toUpperCase`, `slice`, `includes`, `replaceAll` | ts.string-methods | harness, C |
| 20 | objects | литерал объекта, свойства, `interface`, массив значений формы | ts.object, ts.property, ts.interface | harness, C |
| 21 | arrays-of-objects | массив объектов: поиск, группировка, итоги; необязательное свойство `?` | ts.object-iteration, ts.optional-property | harness, C |
| **Блок 4. Надёжность и практика** |||||
| 22 | errors-and-exceptions | `throw new Error`, `try/catch`, `unknown` в `catch`, проверка входных данных | ts.throw, ts.try-catch | harness, C |
| 23 | reading-input | чтение ввода в Node: `import`, `readFileSync(0, "utf8")`, строки, `Number()`, `NaN` | ts.read-input, ts.number-parsing | io, C |
| 24 | mini-project | мини-проект: бюджет из строк ввода (итоги по категориям, плохие строки) | - | io, D |

Что осознанно отсутствует: `any`, `as`-утверждения, `var`, `==`, классы, обобщённые типы, модули кроме одного `import` для ввода,
перечисления, кортежи. Всё это - в `typescript-first-steps` и `typescript-fundamentals`.

Все 24 урока написаны; связи с `typescript-first-steps` применены.

## typescript-for-programmers (уровень 1)

Id `typescript.programmers.<slug>`; упражнение - harness, квиз на ловушки TypeScript для знающих другой язык: типы стираются и данные извне
не проверяются, `number` без `int`, `0.1 + 0.2`, `===` и отсутствие неявных приведений в типах, `string | undefined` после `find`/`at`,
доступ по индексу не даёт `undefined` в типе, `catch (e)` это `unknown`, сравнение массивов и объектов по ссылке, `sort` без компаратора сортирует
строки, структурная совместимость объектов.

| # | slug | assessmentFor (`typescript.from-zero.*`) |
|---|---|---|
| 01 | syntax-and-control-flow | 01-13 |
| 02 | collections-and-functions | 14-21 |
| 03 | errors-and-input | 22, 23 |

## typescript-skill-check

Id `typescript.check.<slug>`; урок-проверка = упражнение (harness) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, типы, условия, циклы, функции | basics | from-zero 01-13 |
| Массивы, объекты, методы массивов | collections | from-zero 14-21 |
| Ошибки и ввод | errors-input | from-zero 22, 23 |
| Вывод типов и структурная типизация | inference-structural | typescript.first-steps.how-typescript-runs, typescript.first-steps.structural-typing-and-narrowing, typescript.fundamentals.inference-and-structural-typing |
| Объединения и сужение | unions-narrowing | typescript.fundamentals.unions-and-narrowing |
| any, unknown, never | unknown-never | typescript.fundamentals.any-unknown-never |
| Обобщения | generics | typescript.fundamentals.generics |
| Утилитарные и отображаемые типы | utility-types | typescript.fundamentals.utility-mapped-conditional |

Точный список и `assessmentFor` проверок на реальные уроки `typescript-first-steps`/`typescript-fundamentals` уточняются в фазе C по их `lesson.yaml`.

## Связи

* `typescript-first-steps`: `requires: [typescript-from-zero]` (вместо `javascript-first-steps`); урок 01 (`typescript.first-steps.how-typescript-runs`)
  вместо `javascript.first-steps.how-javascript-runs` зависит от последнего урока ядра `typescript.from-zero.reading-input` (23)
  и от `typescript.from-zero.objects` (20) (интерфейсы, мост к структурной типизации); применяется в фазе B.
* `typescript-fundamentals`: остаётся `requires: [javascript-fundamentals, typescript-first-steps]`.
* Последний урок `typescript-from-zero` (`mini-project`) ведёт к `typescript.first-steps.how-typescript-runs`.
