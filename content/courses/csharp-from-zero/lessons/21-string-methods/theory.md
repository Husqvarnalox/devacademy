<!-- section: simple -->
## Зачем нужны методы строк

Данные в программы приходят текстом: строка ввода «хлеб 45», имя с лишними пробелами, имя файла «report-2024.txt». Чтобы работать с таким текстом, строку нужно очистить, разрезать на части, найти в ней нужный фрагмент или заменить его. Для этого у строки есть набор готовых методов. Вы уже пользовались `Length`; теперь познакомимся с остальными.

## Одна новая идея: строка неизменяема, методы возвращают новую

Строку `string` нельзя изменить «на месте»: ни один метод не правит исходную строку. Каждый метод **возвращает новую строку**, а старая остаётся как была. Поэтому результат нужно сохранить или использовать:

```csharp
string s = "  кот ";
s.Trim();        // результат потерян, s не изменилась
s = s.Trim();    // верно: s теперь "кот"
```

Основные методы (все вызываются через точку после строки):

| Метод | Что делает |
|---|---|
| `s.Trim()` | убирает пробелы в начале и конце |
| `s.ToUpper()` / `s.ToLower()` | все буквы заглавные / строчные |
| `s.Contains("ab")` | есть ли подстрока (`bool`) |
| `s.Replace("a", "b")` | заменяет все вхождения |
| `s.Substring(start)` | часть строки от позиции `start` до конца |
| `s.Substring(start, length)` | часть длиной `length`, начиная с `start` |
| `s.Split(' ')` | разрезает на массив строк по разделителю |

Отдельный символ строки - значение типа **`char`**: `s[0]` - первый символ. Литерал `char` пишется в **одинарных** кавычках (`'a'`), строка - в двойных (`"a"`). Это разные типы: `'a'` - один символ, `"a"` - строка из одного символа. Позиции в строке, как и в массиве, считаются от нуля.

<!-- section: examples -->
## Пример 1. Trim, регистр, длина

```csharp run
string raw = "   Анна Петрова  ";
string name = raw.Trim();
Console.WriteLine($"[{name}]");
Console.WriteLine(name.ToUpper());
Console.WriteLine(name.ToLower());
Console.WriteLine(name.Length);
Console.WriteLine(raw.Length);
```
```output
[Анна Петрова]
АННА ПЕТРОВА
анна петрова
12
17
```

Разбор:

* `Trim()` убирает три пробела слева и два справа; квадратные скобки в выводе показывают границы строки;
* `ToUpper` и `ToLower` создают новые строки, `name` остаётся прежней;
* у `raw` длина 17, у очищенной `name` - 12.

## Пример 2. Неизменяемость

```csharp run
string s = "кот";
s.ToUpper();
Console.WriteLine(s);
s = s.ToUpper();
Console.WriteLine(s);
```
```output
кот
КОТ
```

Первый вызов `ToUpper()` создал строку «КОТ», но никуда её не записал, и она пропала. После присваивания `s = s.ToUpper();` переменная получает новую строку.

## Пример 3. Contains, Replace, Substring

```csharp run
string file = "report-2024.txt";
Console.WriteLine(file.Contains("2024"));
Console.WriteLine(file.Replace("report", "отчёт"));
Console.WriteLine(file.Substring(0, 6));
Console.WriteLine(file.Substring(7));
```
```output
True
отчёт-2024.txt
report
2024.txt
```

`Substring(0, 6)` берёт 6 символов начиная с позиции 0: `report`. `Substring(7)` берёт всё от позиции 7 (символ `2`) до конца. Оригинальная строка `file` не менялась ни разу.

## Пример 4. Символы

```csharp run
string word = "Привет";
char first = word[0];
char last = word[word.Length - 1];
Console.WriteLine(first);
Console.WriteLine(last);

int digits = 0;
foreach (char c in "a1b22c333")
{
    if (char.IsDigit(c))
    {
        digits++;
    }
}
Console.WriteLine(digits);
```
```output
П
т
6
```

`word[0]` - символ `П` типа `char`, последний символ имеет индекс `Length - 1`. По строке можно пройти циклом `foreach (char c in ...)`. Метод `char.IsDigit(c)` проверяет, цифра ли символ.

## Пример 5. Split: разрезаем строку

```csharp run
string line = "хлеб 45";
string[] parts = line.Split(' ');
string item = parts[0];
int price = int.Parse(parts[1]);
Console.WriteLine($"{item} стоит {price}");

string[] many = "a  b   c".Split(' ');
Console.WriteLine(many.Length);
string[] clean = "a  b   c".Split(' ', StringSplitOptions.RemoveEmptyEntries);
Console.WriteLine(clean.Length);
```
```output
хлеб стоит 45
6
3
```

`Split(' ')` режет строку по каждому пробелу. Для «хлеб 45» получаются два фрагмента. Если пробелов подряд несколько, между ними появляются пустые строки: `"a  b   c"` даёт шесть частей, три из которых пустые. Добавка `StringSplitOptions.RemoveEmptyEntries` отбрасывает пустые части, и остаются три: `a`, `b`, `c`. Для разбора реальных строк ввода почти всегда пишут именно её.

## Пример 6. Числа в одной строке

Ввод:

```
3 5   8
```

```csharp
string line = Console.ReadLine() ?? "";
string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
int sum = 0;
foreach (string part in parts)
{
    sum += int.Parse(part);
}
Console.WriteLine($"Чисел: {parts.Length}, сумма: {sum}");
```

Вывод:

```
Чисел: 3, сумма: 16
```

Так читают несколько чисел из одной строки: разрезать по пробелам, затем превратить каждый фрагмент в число. Лишние пробелы между числами безопасны благодаря `RemoveEmptyEntries`.

**Попробуйте.** Напишите код, который из `"  Мария  "` получает `"МАРИЯ"`. Из строки `"2024-05-17"` получите год, месяц и день: сначала через `Substring`, потом через `Split('-')`. Проверьте, что `"Кот" == "кот"` даёт `False`, а `"Кот".ToLower() == "кот"` даёт `True`.

<!-- section: precise -->
## Точные правила

* Строка неизменяема. Методы `Trim`, `ToUpper`, `ToLower`, `Replace`, `Substring` возвращают новую строку; результат нужно сохранить.
* `s[i]` возвращает `char`; присвоить символ нельзя (`s[0] = 'x'` - ошибка `CS0200`).
* Допустимые позиции: от 0 до `Length - 1`. `s[i]` за границами и `Substring` с неверными аргументами бросают исключения (`IndexOutOfRangeException`, `ArgumentOutOfRangeException`).
* `Substring(start, length)` требует `start + length <= Length`. `Substring(Length)` возвращает пустую строку.
* `Contains` и `Replace` различают регистр: `"Кот".Contains("кот")` равно `false`.
* `Split` возвращает `string[]`. Пустые части получаются при подряд идущих разделителях и при разделителе в начале или в конце; `StringSplitOptions.RemoveEmptyEntries` их убирает. Для пустой строки `"".Split(' ', StringSplitOptions.RemoveEmptyEntries)` возвращает пустой массив.
* `Trim()` без аргументов убирает все пробельные символы (пробелы, табуляции, переводы строк) по краям.
* Сравнение `==` для строк сравнивает содержимое с учётом регистра. Для сравнения без учёта регистра приводят обе строки к одному регистру.
* `char` - одиночный символ в одинарных кавычках; `string` - последовательность символов в двойных.

<!-- section: mistakes -->
## Типичные ошибки

**1. Результат метода не сохранён.**

```csharp
string s = "  Анна ";
s.Trim();
Console.WriteLine($"[{s}]");
```

Вывод `[  Анна ]`: строка не изменилась. Метод вернул новую строку, которую никто не взял. Пишите `s = s.Trim();`.

**2. Выход за границы в `Substring`.**

```csharp
string s = "кот";
Console.WriteLine(s.Substring(5));
```

Программа останавливается: `System.ArgumentOutOfRangeException: startIndex cannot be larger than length of string. (Parameter 'startIndex')`. Для варианта `Substring(0, 1)` на пустой строке сообщение другое: `Index and length must refer to a location within the string. (Parameter 'length')`. Проверяйте длину до вызова.

**3. Первый символ пустой строки.**

```csharp
string s = "";
char c = s[0];
```

Ошибка во время работы: `System.IndexOutOfRangeException: Index was outside the bounds of the array.` В пустой строке нет символа с индексом 0. Сначала проверьте `s.Length > 0`.

**4. Метод без скобок.**

```csharp
string s = "кот";
string t = s.ToUpper;
```

Компилятор: `error CS0428: Cannot convert method group 'ToUpper' to non-delegate type 'string'. Did you intend to invoke the method?` Метод вызывают со скобками: `s.ToUpper()`.

**5. `char` и `string` перепутаны.**

```csharp
char c = "a";
string s = 'a';
```

Компилятор: `error CS0029: Cannot implicitly convert type 'string' to 'char'` для первой строки и `error CS0029: Cannot implicitly convert type 'char' to 'string'` для второй. Одинарные кавычки - символ, двойные - строка.

**6. Попытка изменить символ.**

```csharp
string s = "кот";
s[0] = 'К';
```

Компилятор: `error CS0200: Property or indexer 'string.this[int]' cannot be assigned to -- it is read only`. Соберите новую строку: `s = "К" + s.Substring(1);`.

**7. Пустые части после `Split`.** `"a  b".Split(' ')` содержит пустую среднюю часть, и `int.Parse("")` на ней упадёт с `FormatException`. Используйте `StringSplitOptions.RemoveEmptyEntries`.

**8. Сравнение с учётом регистра.** `"Да" == "да"` - `false`. Если регистр не важен, сравнивайте `answer.ToLower() == "да"`.

<!-- section: summary -->
## Итоги

* Строки неизменяемы: методы возвращают новую строку, результат нужно присвоить или использовать.
* `Trim`, `ToUpper`, `ToLower`, `Contains`, `Replace`, `Substring`, `Split` покрывают бóльшую часть обработки текста.
* `s[i]` даёт `char`; `char` пишется в одинарных кавычках.
* `Split(' ', StringSplitOptions.RemoveEmptyEntries)` - основной способ разобрать строку ввода на части.
* Позиции и границы проверяйте: выход за границы строки - ошибка времени выполнения.

Часть этих ошибок можно предвидеть и обработать, не останавливая программу: [Исключения и TryParse](lesson:csharp.from-zero.exceptions).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `string` в C# неизменяем, как в Java и Python; для многократной сборки используют `StringBuilder`. Строки - ссылочный тип, но `==` сравнивает содержимое (оператор переопределён), в отличие от Java.
* Внутри строка - последовательность UTF-16 кодовых единиц, `char` - одна такая единица. Для символов вне базовой плоскости (эмодзи) один «символ» занимает две единицы; `Length` считает единицы, а не видимые символы. Для кириллицы проблемы нет.
* Методы регистра (`ToUpper`, `ToLower`) используют региональные настройки; для сравнения ключей в «нейтральной» форме есть `ToUpperInvariant` / `ToLowerInvariant` и `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)`.
* `Split(' ', StringSplitOptions.RemoveEmptyEntries)` заменяет `split()` Python без аргументов и `split(/\s+/)` JavaScript для строк с пробелами; для табуляций и других разделителей передают массив `char[]`.
* Срезов `s[1..3]` как в Python нет в курсе, но они существуют (диапазоны, C# 8).
* Интерполяция `$"..."` и `string.Join`/`string.Format` покрывают сборку строк; `+` создаёт новую строку на каждое применение.
* Индекс за границей - исключение, а не `undefined` (JavaScript) и не `""`.

<!-- section: deep-dive -->
## Почему строки неизменяемы

Это необязательный раздел: для заданий он не нужен.

Неизменяемость даёт гарантии: если строка передана в метод, метод не сможет её испортить; строку можно безопасно использовать из нескольких потоков; её хеш-код вычисляется один раз и не меняется, поэтому строка надёжный ключ словаря. Платой становится то, что каждое «изменение» создаёт новый объект.

Для программ, собирающих большой текст по частям, это неудобно: при склейке в цикле каждый шаг копирует всё накопленное. В таких случаях применяют `StringBuilder`: изменяемый буфер, в который дописывают части, а в конце получают итоговую строку. Среда выполнения также хранит одинаковые строки-литералы в одном экземпляре (*интернирование*), поэтому два одинаковых литерала в коде не занимают память дважды.
