<!-- section: simple -->
## Что проверяет эта проверка

Литералы массивов и объектов, индексы и `length`, `for...of`, методы `map`, `filter`, `find`, `includes`, `slice`, `sort` с компаратором, подсчёт и группировка в объектах, ссылки и копирование, областью видимости `let` и `const`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Массивы](lesson:javascript.from-zero.arrays), [Обход массивов](lesson:javascript.from-zero.iterating-arrays), [Стрелочные функции и колбэки](lesson:javascript.from-zero.arrow-functions), [Методы массивов](lesson:javascript.from-zero.array-methods), [Методы строк](lesson:javascript.from-zero.string-methods), [Объекты](lesson:javascript.from-zero.objects), [Массивы объектов](lesson:javascript.from-zero.arrays-of-objects), [Область видимости](lesson:javascript.from-zero.scope).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```javascript run
const players = [
  { name: "Ann", score: 40 },
  { name: "Bob", score: 75 },
  { name: "Cid", score: 75 },
];
const top = players.filter((p) => p.score >= 50).map((p) => p.name);
console.log(top);
const totals = {};
for (const p of players) {
  totals[p.score] = (totals[p.score] ?? 0) + 1;
}
console.log(totals, Object.keys(totals));
const nums = [10, 9, 1];
console.log([...nums].sort(), [...nums].sort((a, b) => a - b), nums);
```
```output
[ 'Bob', 'Cid' ]
{ '40': 1, '75': 2 } [ '40', '75' ]
[ 1, 10, 9 ] [ 1, 9, 10 ] [ 10, 9, 1 ]
```

`filter` и `map` возвращают новые массивы, ключи объекта всегда строки, а `sort` без компаратора сравнивает числа как строки.

<!-- section: precise -->
## Что нужно уметь

* создавать массивы и объекты, читать и менять элементы по индексу и ключу;
* обходить массив через `for...of` и методы;
* применять `map`, `filter`, `find`, `reduce`, `includes`, `slice`;
* сортировать с компаратором и знать, что `sort` меняет массив;
* считать и группировать данные в объекте;
* отличать копию массива от второй ссылки на тот же массив.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `sort()` без компаратора для чисел сортирует как строки.
* `const b = a` не копирует массив: изменение `b` видно в `a`.
* `arr.map(...)` без использования результата ничего не меняет в `arr`.
* Чтение за границей массива даёт `undefined`, а не ошибку.
* В объекте `totals[1]` и `totals["1"]` - один и тот же ключ.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Массивы](lesson:javascript.from-zero.arrays), [Обход массивов](lesson:javascript.from-zero.iterating-arrays), [Стрелочные функции и колбэки](lesson:javascript.from-zero.arrow-functions), [Методы массивов](lesson:javascript.from-zero.array-methods), [Методы строк](lesson:javascript.from-zero.string-methods), [Объекты](lesson:javascript.from-zero.objects), [Массивы объектов](lesson:javascript.from-zero.arrays-of-objects), [Область видимости](lesson:javascript.from-zero.scope). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
