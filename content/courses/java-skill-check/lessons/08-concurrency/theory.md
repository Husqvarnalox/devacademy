<!-- section: simple -->
## Что проверяет эта проверка

`Thread`, `start` и `join`, гонки данных и атомарность (`count++` не атомарна), `synchronized`, `AtomicInteger`, `ExecutorService`, `Future.get`, обязательный `shutdown`, виртуальные потоки.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Потоки, модель памяти, ExecutorService и виртуальные потоки](lesson:java.fundamentals.threads-executors-virtual).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;

public class Main {
    public static void main(String[] args) throws Exception {
        AtomicInteger hits = new AtomicInteger();
        List<Thread> threads = new ArrayList<>();
        for (int i = 0; i < 4; i++) {
            Thread t = new Thread(() -> {
                for (int k = 0; k < 1000; k++) {
                    hits.incrementAndGet();
                }
            });
            threads.add(t);
            t.start();
        }
        for (Thread t : threads) {
            t.join();
        }
        System.out.println(hits.get());

        ExecutorService pool = Executors.newFixedThreadPool(2);
        try {
            Future<Integer> a = pool.submit(() -> 20 + 1);
            Future<Integer> b = pool.submit(() -> 2 * 3);
            System.out.println(a.get() + b.get());
        } finally {
            pool.shutdown();
        }
    }
}
```
```output
4000
27
```
`start()` запускает поток, `join()` ждёт его завершения. `AtomicInteger.incrementAndGet()` выполняет увеличение атомарно, поэтому итог всегда 4000. `submit` возвращает `Future`, а `get()` ждёт результат; `shutdown()` в `finally` освобождает потоки пула, иначе программа не завершится.

<!-- section: precise -->
## Что нужно уметь

* запускать потоки через `start()` и дожидаться их через `join()`;
* объяснять, почему `count++` из нескольких потоков теряет обновления, и исправлять через `AtomicInteger` или `synchronized`;
* разбивать работу на части, отправлять их в `ExecutorService.submit` и собирать результаты через `Future.get()`;
* всегда вызывать `shutdown()` для созданного пула (в `finally`);
* учитывать граничные случаи: частей больше, чем элементов, и пустые данные;
* различать `volatile` (видимость) и атомарность.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `volatile int count` и `count++` из двух потоков: `volatile` даёт видимость, но не атомарность; обновления теряются.
* Пул без `shutdown()`: потоки пула не демоны, программа не завершается.
* Забытый `join()`: результат читается раньше, чем потоки закончили работу.
* `start()` вызывается дважды на одном потоке (`IllegalThreadStateException`) или вместо него вызывается `run()`, и код выполняется в текущем потоке.
* Складывание результатов `Future` в общую переменную из задач без синхронизации.
* Деление списка на части без учёта остатка теряет последние элементы.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Потоки, модель памяти, ExecutorService и виртуальные потоки](lesson:java.fundamentals.threads-executors-virtual). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
