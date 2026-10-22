<!-- section: simple -->
## Что проверяет эта проверка

Массивы и их тип `number[]`, обход через `for...of`, стрелочные функции и методы `map`, `filter`, `find`, `join`, сортировка копии с компаратором, значения `T | undefined`, методы строк, объекты с `interface`, необязательные свойства и массивы объектов.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Массивы](lesson:typescript.from-zero.arrays), [Обход массивов](lesson:typescript.from-zero.iterating-arrays), [Стрелочные функции](lesson:typescript.from-zero.arrow-functions), [Методы массивов](lesson:typescript.from-zero.array-methods), [Необязательные значения](lesson:typescript.from-zero.optional-values), [Методы строк](lesson:typescript.from-zero.string-methods), [Объекты](lesson:typescript.from-zero.objects), [Массивы объектов](lesson:typescript.from-zero.arrays-of-objects).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```typescript run
interface Lot {
  title: string;
  weight: number;
  note?: string;
}
const lots: Lot[] = [
  { title: "яблоки", weight: 12 },
  { title: "груши", weight: 5, note: "хрупкие" },
  { title: "сливы", weight: 8 },
];
const heavy = lots.filter((lot) => lot.weight >= 8).map((lot) => lot.title);
console.log(heavy.join(", "));
const light = lots.find((lot) => lot.weight < 6);
console.log(light?.title ?? "нет", light?.note ?? "без заметки");
const sorted = [...lots].sort((a, b) => a.weight - b.weight);
console.log(sorted.map((lot) => lot.title), lots[0].title);
console.log("  Привет, мир ".trim().toUpperCase().split(", "));
```
```output
яблоки, сливы
груши хрупкие
[ 'груши', 'сливы', 'яблоки' ] яблоки
[ 'ПРИВЕТ', 'МИР' ]
```

Методы `filter`, `map` и `find` принимают стрелочную функцию; `find` возвращает значение или `undefined`, и оператор `?.` вместе с `??` безопасно читает поля. Сортировка идёт по копии `[...lots]`, поэтому исходный массив не меняется.

<!-- section: precise -->
## Что нужно уметь

* объявлять массивы и объекты с типами (`number[]`, `interface`), читать элемент по индексу и обходить массив;
* применять `map`, `filter`, `find`, `includes`, `join` и сортировку с компаратором `(a, b) => a - b`;
* различать методы, меняющие массив (`push`, `sort`), и возвращающие новый (`map`, `filter`, `slice`);
* обрабатывать `undefined` после `find` и `at` и необязательные свойства (`?`, `?.`, `??`);
* вызывать методы строк: `trim`, `split`, `toUpperCase`, `slice`, `includes`, `replaceAll`;
* понимать, что массив и объект передаются по ссылке.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `[10, 9, 1].sort()` без компаратора сортирует как строки и даёт `[1, 10, 9]`.
* `const b = a` не копирует массив: обе переменные ссылаются на один массив.
* Результат `find` без проверки: проверка типов сообщает, что значение `possibly 'undefined'`.
* `for...in` по массиву перебирает индексы-строки, а не значения; нужен `for...of`.
* Условие `if (item.qty)` считает нулевое количество отсутствующим: сравнивайте с `undefined`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Массивы](lesson:typescript.from-zero.arrays), [Обход массивов](lesson:typescript.from-zero.iterating-arrays), [Стрелочные функции](lesson:typescript.from-zero.arrow-functions), [Методы массивов](lesson:typescript.from-zero.array-methods), [Необязательные значения](lesson:typescript.from-zero.optional-values), [Методы строк](lesson:typescript.from-zero.string-methods), [Объекты](lesson:typescript.from-zero.objects), [Массивы объектов](lesson:typescript.from-zero.arrays-of-objects). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
