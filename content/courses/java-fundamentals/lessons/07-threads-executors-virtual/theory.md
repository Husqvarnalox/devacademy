<!-- section: simple -->
## Потоки, общая память и исполнители

**Поток** (`Thread`) - независимая последовательность выполнения со своим стеком. Потоки одного процесса разделяют **кучу**, поэтому могут читать и менять одни и те же объекты. Отсюда главная трудность: если два потока без согласования работают с одним полем, результат зависит от того, как операции чередуются, - возникает **гонка данных** (*data race*).

Три вещи нужно обеспечить при работе с общими данными:

1. **Атомарность** - составная операция (`count++` = прочитать, прибавить, записать) не должна прерываться другим потоком.
2. **Видимость** - запись одного потока должна стать видимой другому; без этого поток может навсегда видеть устаревшее значение из кэша или регистра.
3. **Порядок** - компилятор, JIT и процессор переставляют инструкции; без правил порядок «как в коде» гарантирован только внутри одного потока.

Правила этой гарантии описывает **модель памяти Java** (JMM), а инструменты - `synchronized`, `volatile`, классы `java.util.concurrent`. Руками создавать потоки приходится редко: задачи отправляют **исполнителю** (`ExecutorService`), который управляет пулом потоков. А с Java 21 есть **виртуальные потоки** - очень дешёвые потоки, которые позволяют писать обычный блокирующий код и запускать их десятками тысяч.

<!-- section: precise -->
## Точные правила

**Модель памяти и отношение «происходит до» (happens-before).** Если действие A *происходит до* B, то эффекты A видны в B. Основные правила:

* порядок в программе: действия одного потока упорядочены в порядке кода;
* освобождение монитора (`synchronized`) происходит до последующего захвата того же монитора;
* запись `volatile`-поля происходит до последующего чтения того же поля;
* `Thread.start()` происходит до любого действия запущенного потока; все действия потока происходят до возврата из `join()` на нём;
* завершение конструктора происходит до чтения `final`-полей объекта (безопасная публикация неизменяемых объектов);
* `Future.get()` видит всё, что сделала задача до завершения; отправка задачи исполнителю происходит до её начала.

Если два обращения к переменной (хотя бы одно - запись) **не связаны** отношением happens-before, это гонка данных, и результат не определён: возможны устаревшие значения и невозможные на первый взгляд перестановки. Спецификация допускает даже «разрыв» 64-битных `long` и `double` без `volatile` (на современных 64-битных JVM он не наблюдается, но полагаться на это нельзя).

**`volatile`** даёт видимость и порядок для этой одной переменной, но **не атомарность** составных операций: `volatile int c; c++` по-прежнему теряет обновления. Подходит для флагов остановки и безопасной публикации ссылки.

**`synchronized`** (метод или блок на объекте-мониторе) даёт взаимное исключение и happens-before. **`java.util.concurrent.atomic`** (`AtomicInteger`, `AtomicLong`, `LongAdder`, `AtomicReference`) - атомарные операции без блокировок (`incrementAndGet`, `compareAndSet`). **`ConcurrentHashMap`** допускает одновременные `merge` и `compute` без внешней синхронизации. `ReentrantLock` даёт явные блокировки с `tryLock` и условиями.

**ExecutorService.** Фабрики `Executors`: `newFixedThreadPool(n)`, `newCachedThreadPool()`, `newSingleThreadExecutor()`, `newVirtualThreadPerTaskExecutor()` (Java 21). `submit(Callable<T>)` возвращает `Future<T>`: `get()` ждёт результат; исключение задачи приходит как `ExecutionException` с причиной в `getCause()`. `invokeAll` запускает набор задач и возвращает список завершённых `Future` в порядке отправки. Пул нужно завершить: `shutdown()` и `awaitTermination(...)`; начиная с Java 19 `ExecutorService` реализует `AutoCloseable`, и `try (var ex = ...)` сам вызывает `close()`, ожидая завершения задач. `CompletableFuture` позволяет собирать цепочки: `supplyAsync(...).thenApply(...).thenCombine(...)`, `exceptionally`, `join()`.

**Виртуальные потоки (Java 21, JEP 444).** Это потоки, которыми управляет JVM, а не операционная система; множество виртуальных потоков выполняется на небольшом числе потоков-носителей. Блокирующая операция (сон, ввод-вывод, `BlockingQueue.take`) освобождает носитель. Создание: `Thread.ofVirtual().start(r)`, `Thread.startVirtualThread(r)`, `Executors.newVirtualThreadPerTaskExecutor()`. Правила: виртуальные потоки **не пулируют** (задача - один новый поток), они всегда демоны, имеют обычную семантику `Thread` и модель памяти. Они подходят для задач, **ожидающих ввода-вывода**; для вычислений, нагружающих процессор, выигрыша нет. В Java 21 блокировка внутри `synchronized` закрепляет (*pins*) виртуальный поток за носителем; это ограничение снято в JDK 24 (JEP 491), но для кода под 21 предпочтительнее `ReentrantLock`.

**Версии (проверено компиляцией `--release N`).** `ExecutorService` как `AutoCloseable` - `--release 19`; `Thread.ofVirtual()` и `newVirtualThreadPerTaskExecutor()` - только `--release 21`.

<!-- section: examples -->
## Примеры

**Пример 1. Потерянные обновления и три способа их избежать.** Результат небезопасного счётчика не печатается: он меняется от запуска к запуску; детерминирован только факт `unsafe <= 200000`.

```java run
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.LongAdder;

public class Main {
    static int unsafe = 0;
    static int locked = 0;
    static final Object LOCK = new Object();
    static final AtomicInteger atomic = new AtomicInteger();
    static final LongAdder adder = new LongAdder();

    static void work() {
        for (int i = 0; i < 100_000; i++) {
            unsafe++;
            synchronized (LOCK) {
                locked++;
            }
            atomic.incrementAndGet();
            adder.increment();
        }
    }

    public static void main(String[] args) throws InterruptedException {
        Thread a = new Thread(Main::work);
        Thread b = new Thread(Main::work);
        a.start();
        b.start();
        a.join();
        b.join();
        System.out.println("unsafe <= 200000: " + (unsafe <= 200_000));
        System.out.println(locked + " " + atomic.get() + " " + adder.sum());
    }
}
```
```output
unsafe <= 200000: true
200000 200000 200000
```

После `join()` главный поток видит все записи потоков `a` и `b` (правило happens-before для `join`).

**Пример 2. `volatile` как флаг остановки.**

```java run
public class Main {
    static volatile boolean stop = false;
    static long spins = 0;

    public static void main(String[] args) throws InterruptedException {
        Thread worker = new Thread(() -> {
            long local = 0;
            while (!stop) {
                local++;
                Thread.onSpinWait();
            }
            spins = local;
        });
        worker.start();
        Thread.sleep(50);
        stop = true;
        worker.join();
        System.out.println("worker остановлен: " + (spins > 0));
    }
}
```
```output
worker остановлен: true
```

Без `volatile` у поля `stop` JIT имеет право вынести чтение из цикла, и поток мог бы не остановиться никогда. Число итераций не печатается - оно меняется от запуска к запуску.

**Пример 3. `ExecutorService`, `Future`, `invokeAll` и исключения задач.**

```java run
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

public class Main {
    public static void main(String[] args) throws Exception {
        try (ExecutorService pool = Executors.newFixedThreadPool(3)) {
            List<Future<Integer>> futures = new ArrayList<>();
            for (int i = 1; i <= 5; i++) {
                int n = i;
                futures.add(pool.submit(() -> n * n));
            }
            int sum = 0;
            for (Future<Integer> f : futures) {
                sum += f.get();
            }
            System.out.println("сумма квадратов: " + sum);

            List<Callable<String>> tasks = List.of(() -> "a", () -> "b", () -> "c");
            StringBuilder sb = new StringBuilder();
            for (Future<String> f : pool.invokeAll(tasks)) {
                sb.append(f.get());
            }
            System.out.println(sb);

            Future<Integer> bad = pool.submit(() -> Integer.parseInt("не число"));
            try {
                bad.get();
            } catch (ExecutionException e) {
                System.out.println("причина: " + e.getCause().getClass().getSimpleName());
            }
        }
        System.out.println("пул закрыт");
    }
}
```
```output
сумма квадратов: 55
abc
причина: NumberFormatException
пул закрыт
```

Порядок получения результатов определён порядком `get()`, а не порядком завершения задач, поэтому вывод детерминирован.

**Пример 4. `CompletableFuture`.**

```java run
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class Main {
    public static void main(String[] args) {
        try (ExecutorService pool = Executors.newFixedThreadPool(2)) {
            CompletableFuture<Integer> price = CompletableFuture.supplyAsync(() -> 120, pool);
            CompletableFuture<Integer> qty = CompletableFuture.supplyAsync(() -> 3, pool);
            CompletableFuture<String> total = price.thenCombine(qty, (p, q) -> p * q).thenApply(t -> "итого " + t);
            System.out.println(total.join());

            CompletableFuture<Integer> failing = CompletableFuture.supplyAsync(() -> 10 / (int) Math.floor(0.5), pool)
                    .exceptionally(ex -> -1);
            System.out.println(failing.join());
        }
    }
}
```
```output
итого 360
-1
```

**Пример 5. Виртуальные потоки: десять тысяч одновременно «спящих» задач.**

```java run
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicInteger;

public class Main {
    public static void main(String[] args) throws Exception {
        AtomicInteger started = new AtomicInteger();
        long sum = 0;
        try (ExecutorService ex = Executors.newVirtualThreadPerTaskExecutor()) {
            List<Future<Integer>> results = new ArrayList<>();
            for (int i = 0; i < 10_000; i++) {
                int n = i;
                results.add(ex.submit(() -> {
                    started.incrementAndGet();
                    Thread.sleep(20);
                    return n % 7;
                }));
            }
            for (Future<Integer> f : results) {
                sum += f.get();
            }
        }
        System.out.println(started.get() + " " + sum);

        Thread v = Thread.ofVirtual().name("worker").unstarted(() -> {
            Thread t = Thread.currentThread();
            System.out.println(t.getName() + " virtual=" + t.isVirtual() + " daemon=" + t.isDaemon());
        });
        v.start();
        v.join();
        Thread p = Thread.ofPlatform().name("plain").unstarted(() ->
                System.out.println(Thread.currentThread().getName() + " virtual=" + Thread.currentThread().isVirtual()));
        p.start();
        p.join();
    }
}
```
```output
10000 29994
worker virtual=true daemon=true
plain virtual=false
```

Тот же код на пуле из 10 000 платформенных потоков создал бы тысячи потоков ОС; здесь каждая задача - свой дешёвый виртуальный поток, а `Thread.sleep` освобождает носитель.

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `volatile` делает `count++` атомарным.** Он гарантирует видимость, но не чтение-изменение-запись; нужен `AtomicInteger` или `synchronized`.
* **Читать результат потока без `join()` или `Future.get()`.** Без отношения happens-before вы можете увидеть старое значение или частично записанные данные.
* **Использовать `HashMap`, `ArrayList` из нескольких потоков без синхронизации.** Структура может быть повреждена; применяйте `ConcurrentHashMap` или внешнюю синхронизацию.
* **Проверять время выполнения в тестах.** `Thread.sleep(100)` «чтобы поток успел» - источник нестабильных тестов; ждите событие: `join`, `CountDownLatch`, `Future.get`.
* **Не закрывать пул.** Потоки пула не демоны, и программа не завершится. Используйте `try-with-resources` (Java 19+) или `shutdown`.
* **Проглатывать `InterruptedException`.** Либо пробросьте его, либо верните флаг: `Thread.currentThread().interrupt()`.
* **Пулировать виртуальные потоки или ограничивать их `newFixedThreadPool`.** Они дешёвы; ограничивать нужно доступ к ресурсам (семафор), а не число потоков.
* **Выполнять долгие вычисления в виртуальных потоках в расчёте на ускорение.** Виртуальные потоки помогают при ожидании ввода-вывода, а не при загрузке процессора.
* **Блокироваться внутри `synchronized` в виртуальном потоке (Java 21).** Поток закрепляется за носителем; применяйте `ReentrantLock`.

<!-- section: summary -->
## Итоги

* Общие данные требуют атомарности, видимости и порядка; их даёт отношение happens-before: `synchronized`, `volatile`, `start/join`, `Future.get`.
* `volatile` - для видимости флага, `AtomicInteger` и `ConcurrentHashMap.merge` - для атомарных обновлений.
* Задачи отправляют в `ExecutorService`; результаты получают через `Future.get()` (вывод тогда не зависит от порядка завершения).
* Виртуальные потоки (Java 21) делают блокирующий код масштабируемым: один поток на задачу, без пула; полезны для ввода-вывода.

Дальше: [текст и ввод-вывод](lesson:java.fundamentals.text-and-io).
Назад: [исключения и try-with-resources](lesson:java.fundamentals.exceptions-try-with-resources).
Сравните: [async/await в C#](lesson:csharp.fundamentals.async-await), [горутины и каналы в Go](lesson:go.fundamentals.goroutines-and-channels), [потоки и синхронизация в Python](lesson:python.concurrency.threading-and-synchronization), [fearless concurrency в Rust](lesson:rust.fundamentals.concurrency-fearless).
