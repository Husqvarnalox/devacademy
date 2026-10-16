<!-- section: simple -->
## Строка PHP - это байты

Главное, что нужно знать о строках PHP: **строка - последовательность байтов**, а не символов. Язык не знает, что в ней UTF-8. Поэтому `strlen('Привет')` равно 12 (по два байта на русскую букву), а `strtoupper('привет')` ничего не меняет. Для текста в Юникоде используют расширение `mbstring` (функции `mb_*`): оно считает кодовые точки и умеет менять регистр.

Две ещё темы урока:

* **Форматирование.** `sprintf` собирает строку по шаблону (`%05.2f`, `%-10s`, `%'.10d`), `number_format` расставляет разделители тысяч. Выравнивание в `sprintf` считает **байты**, поэтому для кириллицы и китайского текста нужен `mb_str_pad` (PHP 8.3).
* **Ввод-вывод.** Консольный скрипт читает стандартный ввод через `STDIN` (`fgets`, `stream_get_contents`) и работает с файлами через `file_put_contents`, `file_get_contents`, `file`. Файлы создаются относительно *рабочего каталога* запуска.

<!-- section: precise -->
## Точные правила

**Байты и символы.**

* `strlen`, `substr`, `strtoupper`, `str_pad`, `strrev`, `ucfirst` работают с байтами. На данных ASCII они совпадают с ожидаемым, на UTF-8 - нет: `substr('Привет', 0, 3)` обрежет символ посередине.
* `mb_strlen`, `mb_substr`, `mb_strtoupper`, `mb_strtolower`, `mb_str_split`, `mb_strpos` работают с кодовыми точками. Кодировка по умолчанию задаётся `default_charset` и равна UTF-8, уточнить можно последним аргументом или `mb_internal_encoding`.
* `mb_strtoupper('straße')` даёт `STRASSE`: при смене регистра длина может измениться. `mb_strwidth` считает ширину в ячейках терминала (CJK-символы по 2), что нужно для выравнивания таблиц.
* Один «видимый символ» (графема) может состоять из нескольких кодовых точек (буква с комбинируемым акцентом). `mb_strlen` считает кодовые точки; графемы считает расширение `intl` (`grapheme_strlen`), если оно установлено.
* `mb_trim`, `mb_ltrim`, `mb_rtrim` добавлены в PHP 8.4, `mb_str_pad` - в 8.3. В более старых версиях применяют `preg_replace('/^\s+|\s+$/u', '', $s)` и ручное дополнение. Модификатор `u` включает режим UTF-8 в регулярных выражениях.
* `strcmp` сравнивает байты и подходит для побайтового порядка. `ucfirst` и `ucwords` байтовые, для кириллицы пишут `mb_strtoupper(mb_substr(...)) . mb_substr(...)`.

**sprintf и number_format.**

* Спецификатор: `%[argnum$][флаги][ширина][.точность]тип`. Типы: `d` (целое), `s` (строка), `f`/`F` (дробное), `x`, `X` (шестнадцатеричное), `b` (двоичное), `e`, `%%` (знак процента).
* Флаги: `-` выравнивание влево, `0` дополнение нулями, `+` явный знак, `'c` - любой символ-заполнитель (`%'*8s`). Ширина считается в **байтах**.
* Номер аргумента: `sprintf('%2$s %1$s', 'a', 'b')` даёт `b a`.
* `number_format(1234567.891, 2)` даёт `1,234,567.89`; можно задать разделители: `number_format($n, 2, ',', ' ')`. Функция округляет half away from zero.
* Округление у функций разное: в проверенной сборке PHP 8.5 `printf` округляет ровную половину к чётной цифре (`printf('%.0f', 2.5)` печатает `2`, а `printf('%.0f', 3.5)` - `4`), тогда как `round(2.5)` и `number_format(2.5)` по умолчанию округляют половину от нуля и дают `3`. Поэтому для денег не полагайтесь на форматирование дробных чисел: храните целые копейки и форматируйте через `intdiv` и `%`.

**Стандартный ввод.**

* В CLI определены константы `STDIN`, `STDOUT`, `STDERR`. Построчное чтение: `while (($line = fgets(STDIN)) !== false) { ... }`. `fgets` оставляет перевод строки в конце: применяйте `rtrim($line, "\r\n")` или `trim`.
* Всё сразу: `stream_get_contents(STDIN)` или `file('php://stdin', FILE_IGNORE_NEW_LINES)`. Числа из строки: `fscanf(STDIN, '%d %d')`, `explode`, `preg_split`.
* Конец ввода - `fgets` возвращает `false`. Сравнивайте строго: строка `"0"` ложна при нестрогой проверке.

**Файлы.**

* `file_put_contents($path, $data, FILE_APPEND | LOCK_EX)` записывает или дописывает и возвращает число байтов либо `false`. `file_get_contents($path)` читает целиком. `file($path, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES)` возвращает массив строк.
* `is_file`, `file_exists`, `unlink`, `mkdir($dir, 0777, true)`, `rmdir`, `filesize`, `tempnam(sys_get_temp_dir(), 'x')`.
* Относительный путь считается от текущего рабочего каталога процесса (`getcwd()`), а не от каталога скрипта; путь к каталогу скрипта - `__DIR__`.
* Ошибки открытия чаще всего дают предупреждение и `false`, а не исключение; проверяйте результат. Для работы с большими файлами используйте `fopen`, `fgets`, `fclose`, чтобы не читать всё в память (подходит генератор из прошлого урока).

<!-- section: examples -->
## Примеры

**Пример 1. Байты, символы и ширина.**

```php run
<?php
$s = 'Привет';
echo strlen($s), ' ', mb_strlen($s), PHP_EOL;
echo strtoupper($s), ' ', mb_strtoupper($s), PHP_EOL;
echo mb_substr($s, 0, 3), ' ', implode('|', mb_str_split($s, 2)), PHP_EOL;
echo mb_strtoupper('straße'), ' ', mb_strlen('straße'), PHP_EOL;
echo mb_strwidth('abc'), ' ', mb_strwidth('日本'), ' ', mb_strlen('日本'), PHP_EOL;
echo '[', sprintf('%-8s', 'При'), ']', PHP_EOL;
echo '[', mb_str_pad('При', 8), ']', PHP_EOL;
echo '[', mb_str_pad('При', 8, '.', STR_PAD_LEFT), ']', PHP_EOL;
echo '[', mb_trim("  \u{3000}ok \n"), ']', PHP_EOL;
```
```output
12 6
Привет ПРИВЕТ
При Пр|ив|ет
STRASSE 6
3 4 2
[При  ]
[При     ]
[.....При]
[ok]
```

**Пример 2. sprintf и number_format.**

```php run
<?php
printf("[%5d] [%-5d] [%05d] [%+d]\n", 42, 42, 42, 42);
printf("[%8.3f] [%.0f] [%'*8s] [%-8s]\n", 3.14159, 2.5, 'x', 'ab');
printf("[%x] [%X] [%b] [%o] [%c] [%5.1f%%]\n", 255, 255, 5, 8, 65, 99.5);
printf("%2\$s-%1\$s\n", 'a', 'b');
echo number_format(1234567.891), ' ', number_format(1234567.891, 2), ' ', number_format(1234567.891, 2, ',', ' '), PHP_EOL;
echo number_format(0.5), ' ', number_format(1.5), ' ', number_format(-0.4), PHP_EOL;
echo sprintf('%d.%02d', intdiv(1999, 100), 1999 % 100), PHP_EOL;
echo sprintf('%s', true), '|', sprintf('%s', null), '|', sprintf('%d', '12abc'), PHP_EOL;
```
```output
[   42] [42   ] [00042] [+42]
[   3.142] [2] [*******x] [ab      ]
[ff] [FF] [101] [10] [A] [ 99.5%]
b-a
1,234,568 1,234,567.89 1 234 567,89
1 2 0
19.99
1||12
```

**Пример 3. Чтение построчно из потока.** Скрипту в консоли достаточно заменить `$in` на константу `STDIN`; в примере используется поток в памяти, чтобы результат был воспроизводим.

```php run
<?php
$in = fopen('php://memory', 'w+');
fwrite($in, "10 20\nhello world\n\n  last  ");
rewind($in);

$n = 0;
while (($line = fgets($in)) !== false) {
    $n++;
    $clean = rtrim($line, "\r\n");
    echo $n, ': ', var_export($clean, true), ' len=', strlen($clean), PHP_EOL;
}
var_dump(feof($in));

rewind($in);
[$a, $b] = fscanf($in, '%d %d');
echo $a + $b, PHP_EOL;
echo json_encode(stream_get_contents($in)), PHP_EOL;
fclose($in);
```
```output
1: '10 20' len=5
2: 'hello world' len=11
3: '' len=0
4: '  last  ' len=8
bool(true)
30
"hello world\n\n  last  "
```

**Пример 4. Файлы в рабочем каталоге.**

```php run
<?php
$file = 'notes.txt';
if (is_file($file)) {
    unlink($file);
}

var_dump(file_put_contents($file, "alpha\n"));
file_put_contents($file, "beta\n\ngamma\n", FILE_APPEND | LOCK_EX);
echo json_encode(file($file)), PHP_EOL;
echo json_encode(file($file, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES)), PHP_EOL;
echo filesize($file), ' ', strlen(file_get_contents($file)), PHP_EOL;

$names = [];
$h = fopen($file, 'r');
while (($line = fgets($h)) !== false) {
    $names[] = strtoupper(trim($line));
}
fclose($h);
echo implode(',', array_filter($names)), PHP_EOL;

unlink($file);
var_dump(file_exists($file));
```
```output
int(6)
["alpha\n","beta\n","\n","gamma\n"]
["alpha","beta","gamma"]
18 18
ALPHA,BETA,GAMMA
bool(false)
```

Чтение стандартного ввода в настоящем скрипте выглядит так (в примерах выше stdin недоступен):

```php
<?php
$total = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $total += (int) $line;
}
echo $total, PHP_EOL;
```

<!-- section: mistakes -->
## Типичные ошибки

* **Применять `strlen`, `substr` и `str_pad` к тексту в UTF-8.** Длина и выравнивание окажутся неверными, а `substr` может разрезать символ; берите `mb_*`.
* **Выравнивать таблицу через `sprintf('%-10s')` для не-ASCII.** Ширина считается в байтах: `mb_str_pad` и `mb_strwidth` (для CJK) дают правильное выравнивание.
* **Не убирать `\n` после `fgets`.** Сравнение с `'stop'` не сработает, а при выводе появятся лишние пустые строки; делайте `trim` или `rtrim`.
* **Проверять конец ввода через `while ($line = fgets(...))`.** Строка `"0"` без перевода строки ложна. Сравнивайте с `false`.
* **Считать деньги в `float`.** `0.1 + 0.2 !== 0.3`; храните копейки в `int`.
* **Полагаться на каталог скрипта для относительных путей.** Они считаются от рабочего каталога; для путей рядом со скриптом используйте `__DIR__ . '/file'`.
* **Не проверять результат `file_put_contents` и `fopen`.** Они возвращают `false` и предупреждение, а не исключение.
* **Не удалять временные файлы.** Тест или скрипт, который оставляет файлы в рабочем каталоге, повлияет на следующий запуск.

<!-- section: summary -->
## Итоги

* Строка PHP - байты; для Юникода используйте `mb_*` (`mb_strlen`, `mb_substr`, `mb_strtoupper`, `mb_str_pad` с 8.3, `mb_trim` с 8.4, `mb_strwidth` для ширины).
* `sprintf` форматирует по шаблону и выравнивает по байтам; `number_format` расставляет разделители; деньги храните целыми.
* Ввод читают построчно через `fgets(STDIN)` с проверкой на `false` и `trim`; файлы - через `file_put_contents`, `file`, `unlink` относительно рабочего каталога.

Назад: [пространства имён, автозагрузка и генераторы](lesson:php.fundamentals.namespaces-autoload-generators).
Сравните: [текст и ввод-вывод в Java](lesson:java.fundamentals.text-and-io), [строки в Python](lesson:python.fundamentals.strings), [ошибки и файлы в Python](lesson:python.fundamentals.errors-and-files).
