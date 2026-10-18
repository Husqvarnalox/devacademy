<!-- section: simple -->
## Строки, файлы и ввод

Почти любая программа читает текст, разбирает его и печатает результат. В Java для этого есть несколько слоёв:

* **`String`** - неизменяемая последовательность символов UTF-16; все «изменяющие» методы возвращают новую строку.
* **`StringBuilder`** - изменяемый буфер для сборки строки по частям.
* **`java.nio.file`** (`Path`, `Files`) - современный API файловой системы: чтение и запись одной строкой, обход каталогов.
* **`Scanner`** и **`BufferedReader`** - чтение стандартного ввода: `Scanner` удобен для токенов (числа, слова), `BufferedReader` быстрее и подходит для построчного чтения.

Главные ловушки: путаница `==` и `equals` для строк, сборка строки в цикле через `+`, неверные ожидания от `split`, символы вне базовой плоскости Юникода (эмодзи) и забытая кодировка.

<!-- section: precise -->
## Точные правила

**String.** Строки неизменяемы, поэтому безопасны для общего использования и как ключи `HashMap`; `hashCode` вычисляется один раз и кэшируется. Литералы одинакового содержимого хранятся в пуле строк (`"a" == "a"` истинно), но строка из `new String("a")`, `substring` или `StringBuilder.toString()` - другой объект: сравнивайте через `equals`, без учёта регистра - `equalsIgnoreCase`. Полезные методы Java 11+: `isBlank()`, `strip()` (в отличие от `trim()` понимает пробельные символы Юникода), `lines()`, `repeat(n)`; Java 15 - `formatted(...)` и **текстовые блоки** `"""..."""`; `String.join`, `String.valueOf`, `chars()`.

**Текстовый блок** (Java 15) начинается с `"""` и перевода строки; общий отступ определяется самой левой непустой строкой или закрывающими кавычками и убирается; завершающие пробелы строк удаляются; `\` в конце строки подавляет перевод строки.

**StringBuilder.** `append`, `insert`, `deleteCharAt`, `reverse`, `setLength`, `setCharAt`, `toString`. Конкатенация `+` в цикле создаёт новую строку на каждой итерации (квадратичная работа); внутри одного выражения компилятор оптимизирует её сам. `StringBuilder` не потокобезопасен (потокобезопасный старый аналог - `StringBuffer`).

**split.** `s.split(regex)` принимает **регулярное выражение**: точка и `|` - специальные символы, нужно `"\\."`. Пустые строки в конце результата отбрасываются, если не указан предел: `split(",", -1)` сохраняет их. `"".split(",")` даёт массив из одной пустой строки. Ведущий пустой элемент остаётся (кроме случая пустого совпадения нулевой ширины в начале, Java 8+).

**Юникод.** `char` - это 16-битная единица UTF-16. Символы вне BMP (большинство эмодзи) занимают **суррогатную пару** из двух `char`: `length()` считает единицы UTF-16, `codePointCount(0, length())` - символы (кодовые точки). Для обхода по символам используйте `codePoints()`; `StringBuilder.reverse()` обрабатывает суррогатные пары корректно, а ручной цикл по `charAt` - нет. Перевод между строкой и байтами всегда с явной кодировкой: `getBytes(StandardCharsets.UTF_8)`, `new String(bytes, UTF_8)`. Начиная с Java 18 кодировка по умолчанию - UTF-8 (JEP 400), но для переносимости и в старых версиях кодировку указывают явно.

**java.nio.file.** `Path.of("a", "b.txt")`, `resolve`, `getFileName`, `getParent`, `normalize`. `Files.writeString`, `readString` (Java 11), `readAllLines`, `write`, `lines(path)` (ленивый поток: закрывайте его в try-with-resources!), `exists`, `createTempDirectory`, `createDirectories`, `walk`, `delete`, `copy`, `move`. Ошибки - `IOException` (проверяемое), например `NoSuchFileException`. Потоковые классы закрываются try-with-resources.

**Ввод.** `Scanner(System.in)`: `nextInt`, `nextLong`, `next`, `nextLine`, `hasNext`, `hasNextInt`. `nextInt()` не потребляет перевод строки, поэтому следующий `nextLine()` вернёт пустой остаток строки. Для большого ввода или построчного разбора: `new BufferedReader(new InputStreamReader(System.in, UTF_8))` и `readLine()` (возвращает `null` в конце). Строки разбирайте через `strip`, `indexOf`, `substring`, `split` и регулярные выражения (`Pattern`, `Matcher`); `Integer.parseInt` бросает `NumberFormatException`.

**Версии (проверено `--release N`).** `String.isBlank/strip/lines/repeat` - 11; `Files.readString/writeString` - 11; текстовые блоки и `formatted` - 15. Кодировка по умолчанию UTF-8 - 18 (JEP 400). `format` и `formatted` берут локаль по умолчанию: в русской локали `%05.1f` даст `003,1`, поэтому для воспроизводимого вывода передавайте `Locale.ROOT`, как в примере ниже.

<!-- section: examples -->
## Примеры

**Пример 1. Идентичность, неизменяемость и методы String.**

```java run
import java.util.Locale;

public class Main {
    public static void main(String[] args) {
        String a = "java";
        String b = "ja" + "va";
        String c = new String("java");
        String part = "ja";
        String d = part + "va";
        System.out.println((a == b) + " " + (a == c) + " " + (a == d) + " " + a.equals(d));
        System.out.println(a == c.intern());

        String s = "  Привет, мир \t";
        System.out.println("[" + s.strip() + "] " + s.isBlank() + " " + "   ".isBlank());
        System.out.println("ab".repeat(3) + " " + "a-b-c".replace("-", "+") + " " + "Java".toUpperCase());
        System.out.println(String.join("/", "x", "y", "z") + " " + String.format(Locale.ROOT, "%05.1f", 3.14159) + "|" + "%-4s|%4d".formatted("ab", 42));

        String text = """
            первая
              вторая
            третья\
             (продолжение)
            """;
        System.out.print(text);
        System.out.println(text.lines().count());
    }
}
```
```output
true false false true
true
[Привет, мир] false true
ababab a+b+c JAVA
x/y/z 003.1|ab  |  42
первая
  вторая
третья (продолжение)
3
```

`a == b` истинно, потому что `"ja" + "va"` - константное выражение и вычисляется при компиляции; `d` собирается во время выполнения, поэтому это другой объект. Отступ текстового блока вычисляется по закрывающим `"""`.

**Пример 2. StringBuilder и крайние случаи split.**

```java run
import java.util.Arrays;

public class Main {
    public static void main(String[] args) {
        StringBuilder sb = new StringBuilder();
        for (int i = 1; i <= 5; i++) {
            sb.append(i).append(i < 5 ? "," : "");
        }
        sb.insert(0, "[").append("]");
        System.out.println(sb + " len=" + sb.length());
        sb.setLength(sb.length() - 1);
        sb.deleteCharAt(0);
        System.out.println(sb.reverse());

        System.out.println(Arrays.toString("a,b,,".split(",")));
        System.out.println(Arrays.toString("a,b,,".split(",", -1)));
        System.out.println(Arrays.toString(",a".split(",")));
        System.out.println("".split(",").length + " " + "".split(",", -1).length);
        System.out.println(Arrays.toString("a.b|c".split("\\.")) + " " + Arrays.toString("a|b".split("\\|")));
        System.out.println(Arrays.toString(" a  b ".strip().split("\\s+")));
    }
}
```
```output
[1,2,3,4,5] len=11
5,4,3,2,1
[a, b]
[a, b, , ]
[, a]
1 1
[a, b|c] [a, b]
[a, b]
```

**Пример 3. UTF-16, кодовые точки и байты.**

```java run
import java.nio.charset.StandardCharsets;

public class Main {
    public static void main(String[] args) {
        String s = "Aé€😀";
        System.out.println(s.length() + " " + s.codePointCount(0, s.length()));
        System.out.println(s.getBytes(StandardCharsets.UTF_8).length + " " + s.getBytes(StandardCharsets.UTF_16BE).length);
        s.codePoints().forEach(cp -> System.out.print(Integer.toHexString(cp) + ":" + Character.charCount(cp) + " "));
        System.out.println();
        char first = s.charAt(3);
        System.out.println(Character.isHighSurrogate(first) + " " + Character.isLetter(s.codePointAt(1)));
        byte[] bytes = "Привет".getBytes(StandardCharsets.UTF_8);
        System.out.println(bytes.length + " " + new String(bytes, StandardCharsets.UTF_8));
        System.out.println(new String(bytes, StandardCharsets.ISO_8859_1).length());
        System.out.println(new StringBuilder("a😀b").reverse().codePointAt(1) == 0x1F600);
    }
}
```
```output
5 4
10 10
41:1 e9:1 20ac:1 1f600:2 
true true
12 Привет
12
true
```

**Пример 4. Файлы через java.nio.file.**

```java run
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.List;
import java.util.stream.Stream;

public class Main {
    public static void main(String[] args) throws IOException {
        Path dir = Files.createTempDirectory(Path.of("."), "lesson8");
        Path file = dir.resolve("notes").resolve("todo.txt");
        Files.createDirectories(file.getParent());
        Files.writeString(file, "купить хлеб\nпозвонить\n");
        Files.writeString(file, "сделать урок\n", StandardOpenOption.APPEND);

        List<String> lines = Files.readAllLines(file);
        System.out.println(lines.size() + " " + lines.get(2));
        try (Stream<String> s = Files.lines(file)) {
            System.out.println(s.filter(l -> l.contains("у")).count());
        }
        System.out.println(file.getFileName() + " " + dir.relativize(file));
        System.out.println(Path.of("a/b/../c/./d.txt").normalize());
        try (Stream<Path> walk = Files.walk(dir)) {
            System.out.println(walk.filter(Files::isRegularFile).count());
        }
        Files.delete(file);
        try {
            Files.readString(file);
        } catch (NoSuchFileException e) {
            System.out.println("нет файла: " + file.getFileName());
        }
        Files.delete(file.getParent());
        Files.delete(dir);
        System.out.println(Files.exists(dir));
    }
}
```
```output
3 сделать урок
2
todo.txt notes/todo.txt
a/c/d.txt
1
нет файла: todo.txt
false
```

Имя временного каталога случайное, поэтому программа выводит только имена файлов и числа.

**Пример 5. Scanner, ловушка nextLine и BufferedReader.** Источник - строка, чтобы пример не зависел от ввода.

```java run
import java.io.BufferedReader;
import java.io.IOException;
import java.io.StringReader;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) throws IOException {
        String input = "3 4\nhello world\n7\n";
        Scanner sc = new Scanner(input);
        int a = sc.nextInt();
        int b = sc.nextInt();
        String rest = sc.nextLine();
        String line = sc.nextLine();
        System.out.println(a + b + " [" + rest + "] [" + line + "]");
        while (sc.hasNext()) {
            if (sc.hasNextInt()) {
                System.out.println("int " + sc.nextInt());
            } else {
                System.out.println("word " + sc.next());
            }
        }

        BufferedReader br = new BufferedReader(new StringReader("x=1\n\ny = 2 \n"));
        String l;
        while ((l = br.readLine()) != null) {
            if (l.isBlank()) {
                continue;
            }
            int eq = l.indexOf('=');
            System.out.println(l.substring(0, eq).strip() + " -> " + Integer.parseInt(l.substring(eq + 1).strip()));
        }

        Scanner words = new Scanner("a;b;;c").useDelimiter(";");
        StringBuilder sb = new StringBuilder();
        while (words.hasNext()) {
            sb.append('<').append(words.next()).append('>');
        }
        System.out.println(sb);
    }
}
```
```output
7 [] [hello world]
int 7
x -> 1
y -> 2
<a><b><><c>
```

<!-- section: mistakes -->
## Типичные ошибки

* **Сравнивать строки через `==`.** Результат зависит от пула строк и оптимизаций; используйте `equals`.
* **Собирать большую строку в цикле через `+=`.** Каждая итерация копирует всё накопленное; применяйте `StringBuilder` или `String.join`/`Collectors.joining`.
* **Забыть, что `split` принимает регулярное выражение.** `"a.b".split(".")` вернёт пустой массив; точку и `|` нужно экранировать.
* **Терять пустые поля.** Без `-1` завершающие пустые строки отбрасываются; для CSV это ломает число столбцов.
* **Считать `length()` числом символов.** Для эмодзи и редких иероглифов это число единиц UTF-16; для выравнивания и подсчёта используйте `codePointCount`.
* **Не указывать кодировку.** Без неё результат зависит от окружения (до Java 18); всегда передавайте `StandardCharsets.UTF_8`.
* **`nextInt()` и затем `nextLine()`.** Второй вызов вернёт остаток текущей строки (часто пустой); прочитайте его лишним `nextLine()` или разбирайте всё построчно.
* **Не закрывать `Files.lines`, `Files.walk`, `Files.list`.** Это потоки с открытым дескриптором: оборачивайте в try-with-resources.
* **Ловить `IOException` и молчать.** Сообщение исключения и путь нужны для диагностики; пробрасывайте или оборачивайте с причиной.
* **Использовать `trim()` для Юникода.** Он удаляет только символы с кодом не больше пробела; `strip()` понимает пробелы Юникода.

<!-- section: summary -->
## Итоги

* `String` неизменяема; для сравнения - `equals`, для сборки по частям - `StringBuilder`.
* `split` работает с регулярными выражениями и по умолчанию отбрасывает пустые хвосты (предел `-1` их сохраняет).
* Длина строки - в единицах UTF-16; символы считают через кодовые точки; байты получают только с явной кодировкой.
* `Files` и `Path` читают и пишут файлы целиком; потоки `lines`, `walk` закрывают.
* Для стандартного ввода: `Scanner` для токенов, `BufferedReader` для строк и скорости.

Назад: [потоки и виртуальные потоки](lesson:java.fundamentals.threads-executors-virtual).
Сравните: [строки в Python](lesson:python.fundamentals.strings), [файлы и ошибки в Python](lesson:python.fundamentals.errors-and-files), [pathlib и tempfile](lesson:python.stdlib.pathlib-os-shutil-tempfile), [обработка ошибок в Kotlin](lesson:kotlin.fundamentals.error-handling).
