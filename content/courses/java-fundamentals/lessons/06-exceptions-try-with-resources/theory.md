<!-- section: simple -->
## Исключения: что бросать, что ловить и как не терять ресурсы

**Исключение** - объект, описывающий ошибку, который «летит» вверх по стеку вызовов, пока его кто-нибудь не поймает. Если никто не поймал, поток завершается и печатает трассу стека.

В Java исключения делятся на две группы, и это отличает язык от большинства других:

* **Проверяемые** (*checked*): наследники `Exception`, кроме `RuntimeException`. Компилятор требует либо поймать такое исключение, либо объявить его в сигнатуре метода (`throws`). Примеры: `IOException`, `SQLException`. Идея: ошибки, которых можно ожидать от внешнего мира (файл не найден), должны быть видны в сигнатуре.
* **Непроверяемые** (*unchecked*): `RuntimeException` и его наследники (`NullPointerException`, `IllegalArgumentException`, `ArrayIndexOutOfBoundsException`) и `Error` (`OutOfMemoryError`, `StackOverflowError`). Они отражают ошибки программиста или неисправимые сбои; обрабатывать их везде не требуется.

Вторая половина темы - **ресурсы**: файлы, соединения, потоки нужно закрывать даже при ошибке. В Java для этого служит конструкция **`try-with-resources`**: ресурс объявляется в скобках после `try`, и компилятор сам гарантирует вызов `close()`.

<!-- section: precise -->
## Точные правила

**Иерархия.** `Throwable` → `Error` и `Exception`; `Exception` → `RuntimeException` и прочие (проверяемые). `catch (Exception e)` ловит и проверяемые, и `RuntimeException`, но не `Error`. Обработчики проверяются сверху вниз: сначала более частные типы. Если вы поставите `catch (Exception e)` перед `catch (IOException e)`, второй блок недостижим - ошибка компиляции. Несколько типов в одном блоке: `catch (IOException | SQLException e)` (типы не должны быть связаны наследованием; `e` неявно финальна).

**Объявление.** Метод, который может выбросить проверяемое исключение, объявляет его в `throws`. Переопределяющий метод не может объявить *более широкие* проверяемые исключения, чем родитель. Лямбды в стандартных функциональных интерфейсах проверяемых исключений не объявляют, поэтому их оборачивают: `throw new UncheckedIOException(e)`.

**Цепочки.** Исключение хранит причину: `new ServiceException("не удалось загрузить", cause)`, затем `getCause()`. Не теряйте причину - иначе пропадёт настоящая трасса.

**`finally`.** Блок выполняется после `try` и `catch` при любом исходе (включая `return` и исключение). Нюансы: значение `return` вычисляется *до* `finally`; `return` или `throw` внутри `finally` подменяют результат и **поглощают** исходное исключение; `finally` не выполнится, если JVM остановлена (`System.exit`, аварийное завершение).

**`try-with-resources`.** Ресурс должен реализовать `AutoCloseable` (метод `close()`). Правила:

* ресурсы закрываются в порядке, **обратном объявлению**, после тела `try` и **до** выполнения блоков `catch` и `finally`;
* если и тело, и `close()` выбросили исключения, «главным» становится исключение из тела, а исключения из `close()` добавляются к нему как **подавленные** (`getSuppressed()`); без этой конструкции `close()` в `finally` затёр бы исходную ошибку;
* с Java 9 в скобках можно указать уже существующую эффективно финальную переменную: `try (res) { ... }`;
* если ресурс при создании выбросил исключение, закрываются только уже созданные ресурсы.

**Практика проектирования.** Бросайте `IllegalArgumentException` и `IllegalStateException` для нарушенных предусловий; проверяемые исключения - там, где вызывающий код реально может восстановиться. Пустые `catch` недопустимы. Не используйте исключения для обычного управления потоком: создание исключения с трассой дорого.

**Полезные сообщения.** С Java 14 (JEP 358) `NullPointerException` указывает, что именно было `null` (в Java 15 включено по умолчанию). Имена локальных переменных попадают в сообщение, только если класс скомпилирован с `-g`; иначе вы увидите `<local1>`.

<!-- section: examples -->
## Примеры

**Пример 1. Иерархия, порядок `catch` и сообщения.**

```java run
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;

public class Main {
    static String name;

    static void risky(int mode) throws IOException {
        switch (mode) {
            case 0 -> Files.readString(Path.of("no-such-file.txt"));
            case 1 -> Integer.parseInt("abc");
            case 2 -> System.out.println(10 / (mode - 2));
            case 3 -> {
                int[] arr = new int[3];
                arr[mode] = 1;
            }
            case 4 -> System.out.println(name.length());
            default -> throw new IllegalStateException("режим " + mode);
        }
    }

    public static void main(String[] args) {
        for (int mode = 0; mode <= 5; mode++) {
            try {
                risky(mode);
            } catch (NoSuchFileException e) {
                System.out.println("NoSuchFileException: " + e.getMessage());
            } catch (IOException e) {
                System.out.println("IOException");
            } catch (NumberFormatException | ArithmeticException e) {
                System.out.println(e.getClass().getSimpleName() + ": " + e.getMessage());
            } catch (NullPointerException e) {
                System.out.println("NPE: " + e.getMessage());
            } catch (RuntimeException e) {
                System.out.println(e.getClass().getSimpleName() + ": " + e.getMessage());
            }
        }
        System.out.println(RuntimeException.class.isAssignableFrom(IllegalStateException.class));
        System.out.println(RuntimeException.class.isAssignableFrom(IOException.class));
    }
}
```
```output
NoSuchFileException: no-such-file.txt
NumberFormatException: For input string: "abc"
ArithmeticException: / by zero
ArrayIndexOutOfBoundsException: Index 3 out of bounds for length 3
NPE: Cannot invoke "String.length()" because "Main.name" is null
IllegalStateException: режим 5
true
false
```

`NoSuchFileException` - потомок `IOException`, поэтому его блок стоит выше. Для `null` сообщение (с Java 14) называет, что именно оказалось пустым: статическое поле `Main.name`. Блок для `NullPointerException` стоит выше общего `RuntimeException`.

**Пример 2. `finally`, `return` и подмена исключения.**

```java run
public class Main {
    static int a() {
        int x = 1;
        try {
            return x;
        } finally {
            x = 2;
            System.out.println("finally a, x=" + x);
        }
    }

    @SuppressWarnings("finally")
    static int b() {
        try {
            throw new RuntimeException("потеряно");
        } finally {
            return 42;
        }
    }

    static String c() {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 3; i++) {
            try {
                if (i == 1) continue;
                sb.append("t").append(i);
            } finally {
                sb.append("f").append(i);
            }
        }
        return sb.toString();
    }

    public static void main(String[] args) {
        System.out.println(a());
        System.out.println(b());
        System.out.println(c());
    }
}
```
```output
finally a, x=2
1
42
t0f0f1t2f2
```

**Пример 3. `try-with-resources`: порядок закрытия и подавленные исключения.**

```java run
public class Main {
    static class Res implements AutoCloseable {
        private final String name;
        private final boolean failOnClose;

        Res(String name, boolean failOnClose) {
            this.name = name;
            this.failOnClose = failOnClose;
            System.out.println("open " + name);
        }

        @Override
        public void close() {
            System.out.println("close " + name);
            if (failOnClose) {
                throw new IllegalStateException("close " + name);
            }
        }
    }

    public static void main(String[] args) {
        try (Res a = new Res("A", false); Res b = new Res("B", false)) {
            System.out.println("body");
        }
        System.out.println("---");
        try (Res a = new Res("A", true); Res b = new Res("B", true)) {
            throw new RuntimeException("body");
        } catch (RuntimeException e) {
            System.out.println("catch " + e.getMessage());
            for (Throwable s : e.getSuppressed()) {
                System.out.println("suppressed " + s.getMessage());
            }
        } finally {
            System.out.println("finally");
        }
    }
}
```
```output
open A
open B
body
close B
close A
---
open A
open B
close B
close A
catch body
suppressed close B
suppressed close A
finally
```

Порядок: `catch` выполняется *после* закрытия обоих ресурсов; главным остаётся исключение тела, а оба сбоя `close()` попали в подавленные (в порядке закрытия: сначала `B`).

**Пример 4. Собственные исключения, цепочка причин и обёртка проверяемого.**

```java run
import java.io.IOException;
import java.io.UncheckedIOException;
import java.util.List;

public class Main {
    static class ConfigException extends Exception {
        ConfigException(String message, Throwable cause) {
            super(message, cause);
        }
    }

    static String read(String path) throws IOException {
        throw new IOException("диск недоступен: " + path);
    }

    static String load(String path) throws ConfigException {
        try {
            return read(path);
        } catch (IOException e) {
            throw new ConfigException("не удалось загрузить конфигурацию", e);
        }
    }

    public static void main(String[] args) {
        try {
            load("app.conf");
        } catch (ConfigException e) {
            System.out.println(e.getMessage());
            System.out.println(e.getCause().getClass().getSimpleName() + ": " + e.getCause().getMessage());
        }
        try {
            List.of("a", "b").forEach(p -> {
                try {
                    read(p);
                } catch (IOException e) {
                    throw new UncheckedIOException(e);
                }
            });
        } catch (UncheckedIOException e) {
            System.out.println("лямбда: " + e.getCause().getMessage());
        }
    }
}
```
```output
не удалось загрузить конфигурацию
IOException: диск недоступен: app.conf
лямбда: диск недоступен: a
```

**Пример 5. Раскрутка стека: `finally` на пути вверх и трасса.**

```java run
public class Main {
    static void level3() {
        throw new IllegalArgumentException("глубоко");
    }

    static void level2() {
        try {
            level3();
        } finally {
            System.out.println("level2 cleanup");
        }
    }

    static void level1() {
        try {
            level2();
        } catch (IllegalArgumentException e) {
            System.out.println("level1 поймал: " + e.getMessage());
            StackTraceElement top = e.getStackTrace()[0];
            System.out.println(top.getMethodName() + " <- " + e.getStackTrace()[1].getMethodName());
        }
    }

    public static void main(String[] args) {
        level1();
        System.out.println("после");
        assert false : "assert отключён по умолчанию";
        System.out.println("assert не сработал");
    }
}
```
```output
level2 cleanup
level1 поймал: глубоко
level3 <- level2
после
assert не сработал
```

<!-- section: mistakes -->
## Типичные ошибки

* **Пустой `catch` или `catch (Exception e) {}`.** Ошибка исчезает бесследно. Минимум - залогировать с причиной; лучше - пробросить.
* **Ловить слишком широко.** `catch (Throwable)` перехватывает и `OutOfMemoryError`; ловите конкретные типы.
* **Терять причину.** `throw new RuntimeException("ошибка")` вместо `throw new RuntimeException("ошибка", e)` стирает исходную трассу.
* **`return` или `throw` внутри `finally`.** Он поглощает исходное исключение; чистку делайте без возврата значений.
* **Закрывать ресурсы вручную в `finally`.** Исключение из `close()` затирает исходное; используйте `try-with-resources`.
* **Неправильный порядок `catch`.** Широкий тип перед узким - ошибка компиляции.
* **Использовать исключения для штатных ситуаций** (конец ввода, ключ не найден). Для этого есть возвращаемые значения (`Optional`, `null`-проверки, `hasNext`).
* **Объявлять `throws Exception` на всех методах.** Сигнатура перестаёт нести информацию; объявляйте конкретные типы.
* **Рассчитывать на `assert` в рабочем коде.** По умолчанию проверки выключены (включаются флагом `-ea`).

<!-- section: summary -->
## Итоги

* Проверяемые исключения (`Exception` без `RuntimeException`) требуют `catch` или `throws`; непроверяемые (`RuntimeException`, `Error`) - нет.
* `catch` проверяются сверху вниз, частные типы выше общих; причину исключения сохраняйте через конструктор с `cause`.
* `try-with-resources` закрывает ресурсы в обратном порядке до `catch` и `finally` и сохраняет ошибки `close()` как подавленные.
* `return` в `finally` подменяет результат и скрывает исключение.

Дальше: [модель памяти, потоки и виртуальные потоки](lesson:java.fundamentals.threads-executors-virtual).
Назад: [Streams, лямбды и Optional](lesson:java.fundamentals.streams-lambdas-optional).
Сравните: [исключения и ресурсы в C#](lesson:csharp.fundamentals.exceptions-and-resources), [ошибки как значения в Go](lesson:go.fundamentals.functions-and-errors), [обработка ошибок в Rust](lesson:rust.fundamentals.error-handling), [менеджеры контекста в Python](lesson:python.advanced.context-managers).
