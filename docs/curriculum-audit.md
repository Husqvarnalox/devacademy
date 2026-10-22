# Педагогический аудит учебного контента DevAcademy

Дата аудита: 2026-10-22. Объём: `content/catalog.yaml`, 44 курса, 268 каталогов уроков (267 перечислены в `lessons:` курсов + `python-advanced/lessons/00-placement`), 26 путей в `content/paths/`.

Метод: прочитаны `lesson.yaml` (метаданные, objectives, prerequisites, названия вопросов квиза, названия и условия упражнений) для всех уроков через сводную выгрузку; `theory.md` читалась выборочно (первые уроки курсов и места, где требовалось подтвердить находку); связи `prerequisites` проверены скриптом (разрешение id, порядок, межкурсовые ссылки). Тесты упражнений, solution и starter проверялись точечно. Квизы всех 268 уроков целиком не сверялись с теорией: утверждения про квизы ограничены выборкой (см. §6).

Пометка **(оценка)** означает суждение аудитора, а не факт из файлов. Всё остальное подтверждено путями и цитатами.

Справка по семантике уровней: `CURRICULUM.md` §2 определяет `level: 0` как «Первый запуск: как выполняется программа, среда, ввод-вывод», а не как «с нуля». Это объясняет, откуда берутся курсы вида «как Go компилируется + bufio». Но каталог (`catalog.yaml`) и пути (`content/paths/go-backend-developer.yaml` и т. п.) подают эти курсы как первую ступень пути, а `level: 0` и `difficulty: intro` читаются новичком как «можно начинать без знаний». Разрыв между заявленным и реальным уровнем и есть главная проблема.

---

## 1. Инвентаризация

### 1.1. Сводка

* Треков: 40. `status: available`: 27. `status: planned`: 13.
* Planned-треки (из `catalog.yaml`): **angular, fastify, gin, laravel, llm-genai, ml-math, mlops, nestjs, nextjs, nuxt, qt, symfony, vue**. Курсов у них нет.
* Курсов: 44. Уроков: 267 в `course.yaml` + 1 placement-урок (`python-advanced/00-placement`, в `lessons:` не перечислен, по документации так задумано).
* First-steps курсов: 10 (cpp, csharp, go, java, javascript, kotlin, nodejs, php, rust, typescript), всего 22 урока.
* Курс «с нуля для человека, который никогда не программировал» строго один: `python-fundamentals` (его `description`: «для тех, кто начинает с нуля»; путь `content/paths/python-from-zero.yaml`).

### 1.2. Таблица курсов

Колонка «С нуля?»: «да» — реально подходит человеку без опыта программирования; «нет» — предполагает опыт на любом языке.

| Курс | Трек | Lvl | requires | Уроков | Первый урок | С нуля? |
|---|---|---|---|---|---|---|
| python-fundamentals | python | 0 | - | 10 | Что такое программа и как Python её запускает | да (с оговорками §3.1) |
| go-first-steps | go | 0 | - | 3 | Как выполняется Go: go build, рантайм и ввод-вывод | нет |
| cpp-first-steps | cpp | 0 | - | 2 | Как выполняется C++: компиляция, компоновка и cin/cout | нет |
| csharp-first-steps | csharp | 0 | - | 2 | Как выполняется C#: IL, CLR и JIT | нет |
| java-first-steps | java | 0 | - | 2 | Как выполняется Java: javac, байткод и JVM | нет |
| javascript-first-steps | javascript | 0 | - | 2 | Как выполняется JavaScript и как читать stdin | нет |
| kotlin-first-steps | kotlin | 0 | - | 2 | Как выполняется Kotlin: kotlinc, JVM и ввод-вывод | нет |
| php-first-steps | php | 0 | - | 2 | Как выполняется PHP: Zend Engine, опкоды и ввод-вывод | нет |
| rust-first-steps | rust | **1** | - | 2 | Как выполняется Rust: rustc, нет рантайма и stdin | нет |
| typescript-first-steps | typescript | **1** | javascript-first-steps | 2 | Как выполняется TypeScript: tsc, стирание типов | нет |
| nodejs-first-steps | nodejs | **1** | javascript-first-steps | 3 | Node.js как среда: process, argv, stdin | нет |
| go-fundamentals | go | 1 | go-first-steps | 8 | Типы, нулевые значения и строки | нет |
| cpp-fundamentals | cpp | 1 | cpp-first-steps | 8 | Модель компиляции, время жизни и хранение | нет |
| csharp-fundamentals | csharp | 1 | csharp-first-steps | 8 | Система типов и память: упаковка, ref/out, Span | нет |
| java-fundamentals | java | 1 | java-first-steps | 8 | JVM, байткод, типы и память | нет |
| javascript-fundamentals | javascript | 1 | javascript-first-steps | 8 | Типы и приведение типов | нет |
| kotlin-fundamentals | kotlin | 1 | kotlin-first-steps | 8 | Типы, val/var и null-безопасность | нет |
| php-fundamentals | php | 1 | php-first-steps | 8 | Жизненный цикл скрипта, zval и семантика значений | нет |
| rust-fundamentals | rust | 1 | rust-first-steps | 8 | Владение и перемещение в глубину | нет |
| nodejs-fundamentals | nodejs | 1 | nodejs-first-steps, javascript-fundamentals | 8 | Архитектура Node.js: V8, libuv, цикл событий | нет |
| typescript-fundamentals | typescript | 2 | javascript-fundamentals, typescript-first-steps | 7 | Аннотации, вывод типов, структурная типизация | нет |
| algorithms-foundations | algorithms | 1 | - (!) | 6 | Сложность алгоритма и O-нотация | нет (язык не указан, §5.4) |
| python-advanced | python | 2 | python-fundamentals | 8 (+placement) | Функции как объекты и замыкания | нет |
| ml-foundations | ml-classical | 2 | python-fundamentals | 4 | Что такое машинное обучение | нет |
| react-fundamentals | react | 2 | javascript-fundamentals | 6 | Что такое React: компоненты и JSX | нет |
| express-fundamentals | express | 2 | javascript-fundamentals, nodejs-fundamentals | 7 | Устройство Express | нет |
| spring-boot-fundamentals | spring-boot | 2 | java-fundamentals | 7 | Контроллеры и маршрутизация | нет |
| aspnetcore-fundamentals | aspnet-core | 2 | csharp-fundamentals | 7 | Minimal APIs и маршрутизация | нет |
| python-stdlib | python | 3 | python-fundamentals | 8 | collections | нет |
| python-numpy | python | 3 | python-fundamentals, python-advanced | 6 | ndarray и список | нет |
| python-pandas | python | 3 | python-numpy | 6 | Series и DataFrame | нет |
| python-automation | python | 3 | python-stdlib | 6 | Автоматизация файлов | нет |
| databases-sql | databases | 3 | python-fundamentals | 7 | Реляционная модель и SELECT | нет (оценка: по теме вводный, но закрыт знанием Python) |
| os-fundamentals | operating-systems | 3 | python-fundamentals | 8 | Что делает ОС: режимы ядра, системные вызовы | нет |
| networking-fundamentals | networking | 3 | python-fundamentals | 8 | Уровни сети, инкапсуляция | нет |
| security-fundamentals | security | 3 | python-fundamentals | 8 | Модель угроз и принципы защиты | нет |
| devops-fundamentals | devops | 3 | python-fundamentals | 8 | Что такое DevOps: CI/CD и DORA | нет |
| django-fundamentals | django | 3 | python-advanced, databases-sql | 7 | Архитектура Django, настройки и маршрутизация | нет |
| fastapi-fundamentals | fastapi | 3 | python-advanced | 7 | Зачем FastAPI и как устроен ASGI | нет |
| ml-classical-numpy | ml-classical | 3 | ml-foundations, python-numpy | 4 | Векторизованная линейная регрессия | нет |
| go-ml-gomlx | ml-go | 3 | go-fundamentals, ml-foundations | 6 | Тензоры и бэкенд | нет |
| python-concurrency | python | 4 | python-advanced | 7 | Модели конкурентности | нет |
| pytorch-fundamentals | deep-learning | 4 | ml-foundations, ml-classical-numpy | 7 | Тензоры | нет |
| system-design-fundamentals | system-design | 4 | python-fundamentals | 8 | Требования и оценка нагрузки | нет |

Сумма уроков по таблице: 267 (+1 placement).

### 1.3. Что получилось

* «С нуля» реализовано только для Python. Для остальных 9 языков нет ни одного курса, который объясняет переменные, условия, циклы, функции, строки, коллекции: в `theory.md` первых уроков слова «цикл», «функция», «условие» встречаются 0-4 раза и только мимоходом (§2.5).
* Девять из десяти first-steps курсов открываются **одним и тем же упражнением** «Статистика по числам из stdin» (sum/min/max/avg, `n` чисел, разбросанных по строкам, `|x| <= 2 000 000 000`, четыре строки вывода с `avg` на 2 знака): `go`, `cpp`, `csharp`, `java`, `javascript`, `kotlin`, `php`, `rust`; в `typescript` вариант «Статистика по числам в типизированном коде». Только `nodejs-first-steps` начинается с «Мини-wc». Поле `exercise.title` в `content/courses/<lang>-first-steps/lessons/01-*/lesson.yaml`.

---

## 2. Подтверждение или опровержение известных проблем

### 2.1. go-first-steps — ПОДТВЕРЖДЕНО (критично)

**Урок 1** `content/courses/go-first-steps/lessons/01-how-go-runs/lesson.yaml` (`difficulty: intro`, 30 мин, 4 вопроса квиза, 7 тестов упражнения, из них 4 скрытых).

* Цель урока: «Понять, что делает go build, и написать программу, читающую числа через bufio и fmt.Fscan».
* Objectives смешивают пять тем: «Go компилируется в машинный код с встроенным рантаймом»; «Читать stdin через bufio.NewReader и fmt.Fscan»; «Различать int, int32, int64 и понимать молчаливое переполнение»; «Форматировать вывод через fmt.Printf».
* Квиз: «Что создаёт go build для пакета main?», «Что произойдёт, если импортировать пакет и не использовать его?», «Что верно?» (молчаливое переполнение int32, `7 / 2`, смешивание int/int64, локаль у `Printf`), «Какую функцию использовать с bufio.Reader для чтения значения из ввода?» (ответ `fmt.Fscan`).
* Упражнение «Статистика по числам из stdin» требует одновременно: ввод через `bufio.NewReader(os.Stdin)` + `fmt.Fscan`, цикл, инициализацию min/max первым элементом, накопление в `int64`, приведение к `float64` до деления, `%.2f`. Скрытые тесты: `negatives`, `whitespace`, `rounding`, `big-sum` (сумма 4 000 000 000, не влезает в int32).
* В `theory.md` секция `simple` (первый экран) рассказывает про компиляцию, `package main`, `import "fmt"` и `fmt.Scan`; `bufio.NewReader`, `fmt.Fscan`, `io.EOF`, цикл `for`, `int32`/`int64`, `error` как значение появляются только в секциях `precise` («Подробнее») и `examples`.
* Вывод: человек без опыта получает за 30 минут компиляцию, рантайм, пакеты, импорт, переменные, типы, переполнение, цикл, деление, форматирование, буферизованный ввод. Это не «intro» по реальным требованиям.

**Урок 2** `go-first-steps/lessons/02-slices-len-cap-and-append/lesson.yaml` (`intro`, 40 мин) — ПОДТВЕРЖДЕНО.

* Objectives: «Описать срез как {ptr, len, cap} над массивом»; «Объяснить, когда append пишет в общий массив, а когда выделяет новый»; «Использовать copy и тройной срез для независимости»; «Распознавать порчу данных при удалении через append(s[:i], s[i+1:]...)».
* `theory.md`: «Срез - заголовок `{ptr, len, cap}`», «Тройной срез `s[i:j:k]`», «Утечка памяти» маленького среза от большого массива, «Рост» как деталь реализации. Этот материал отдельно разбирается в `go-fundamentals` урок 05 (§4).
* Упражнение «Удаление элемента без порчи оригинала»: требуется понимать алиасинг общего массива и сделать независимую копию.

**Урок 3** `go-first-steps/lessons/03-packages-and-go-test/lesson.yaml` (`intro`, 45 мин, `evaluation: command`) — ПОДТВЕРЖДЕНО.

* Objectives: «Объяснить, чем пакет отличается от модуля и что лежит в go.mod»; «Вернуть из функции ошибку и обернуть её через fmt.Errorf с %w»; «Сравнивать ошибки через errors.Is, а не по тексту»; «Прочитать табличный тест с t.Run». Mastery: «Вы объявляете сентинел-ошибки пакета и оборачиваете их с контекстом»; «Вы пишете табличный тест с подтестами».
* Упражнение «Пакет shop: резерв товара»: `Reserve(stock map[string]int, item string, qty int) (int, error)` и вторая функция, три sentinel-ошибки (`ErrBadQuantity`, `ErrUnknownItem`, `ErrInsufficient`), тесты вида `TestReserve/ok`.
* Фактическая проблема: упражнение использует **карту** (`map[string]int`), а карты как тема появляются в `go-fundamentals/05-maps-and-slices-deep-dive`. Обёртка `%w` и `errors.Is` — новая тема `go-fundamentals/02-functions-and-errors` (objective «Оборачивать ошибки через fmt.Errorf с %w и находить причину через errors.Is и errors.As»).

### 2.2. javascript-first-steps — ПОДТВЕРЖДЕНО

`content/courses/javascript-first-steps/lessons/01-how-javascript-runs/lesson.yaml`.

* Первый objective: «Объяснить роли разбора, байткода (Ignition) и JIT-компиляции (TurboFan) в V8». В `theory.md` слова `V8` (5 раз), `Ignition` (2), `TurboFan` (2), `JIT` (1).
* Первый вопрос квиза: «Что V8 делает с кодом, который выполняется очень часто?» — устройство движка проверяется раньше, чем ученик написал программу.
* Stdin на первом же уроке: starter `exercise/starter/main.js` начинается с `const text = require("node:fs").readFileSync(0, "utf8");` — `require`, CommonJS, `node:fs`, файловый дескриптор `0` до объяснения функций, массивов, модулей. Теория: «`0` - номер файлового дескриптора stdin».
* Остальные objectives: различие `stdout`/`stderr`; представление чисел, `Number()`, `parseInt`, `toFixed`. Упражнение то же «Статистика по числам из stdin».
* Урок 2 `02-values-and-equality` (`intro`, 35 мин): рекурсивный `deepEqual` для JSON-значений с `null`, массивами и объектами. Для новичка это рекурсия + ссылки + приведение типов; для программиста нормально.
* Node-специфика (stdin через `readFileSync`) смешана с языком, хотя описание трека в `catalog.yaml` декларирует «JavaScript как таковой, независимо от среды запуска». Сам урок это признаёт («Он не определяет ни console.log, ни чтение файлов, ни stdin: это предоставляет хост»), но упражнение всё равно строит на Node.

### 2.3. csharp-first-steps — ПОДТВЕРЖДЕНО

`content/courses/csharp-first-steps/lessons/01-how-csharp-runs/lesson.yaml`.

* Objectives: «Описать два этапа: компиляция в IL и JIT-компиляция средой CLR; назвать ReadyToRun и Native AOT»; «Объяснить многоуровневую компиляцию и управляемую память»; «Читать stdin и писать в stdout через Console, форматировать числа с InvariantCulture»; «Предсказывать поведение int при переполнении, checked и целочисленного деления».
* `theory.md`: заголовки «C# компилируется дважды: сначала в IL, потом в машинный код» и «Что делает каждый этап и что из этого следует». Квиз: «Что находится внутри собранного файла Program.dll?», «Чему равно int.MaxValue после x++ в непроверяемом контексте?», short-answer «Какой метод класса Console читает весь стандартный ввод до конца? (имя Console.In.… без скобок)».
* В теории (1009 слов) слова «цикл», «условие», «функция» отсутствуют вовсе (0 вхождений), но упражнение требует цикл и условие.
* Урок 2 `02-value-types-and-nullable` (`intro`, 40 мин): упражнение «Счёт со снимками» — интерпретатор команд `deposit/snapshot/restore/show` с семантикой копирования struct и суммами > 32 бит. Темы: значимые/ссылочные типы, `struct` vs `class`, `Nullable<T>`, `??`, `?.`, `is { } v`, «nullable reference types - аннотации компилятора».

### 2.4. python-fundamentals урок 1 — ПОДТВЕРЖДЕНО ЧАСТИЧНО (major, но мягче остальных)

`content/courses/python-fundamentals/lessons/01-how-python-runs/lesson.yaml`, `theory.md`.

* Подтверждено: objectives — «Назвать этапы работы CPython - компиляция в байткод и выполнение виртуальной машиной»; «Отличить стандартный вывод (stdout) от потока ошибок (stderr)»; «Управлять выводом print с помощью параметров sep и end». В теории раздел «От исходника к результату: что делает CPython» (байткод, виртуальная машина, `__pycache__/*.pyc`), абзац «Потоки вывода» (stdin/stdout/stderr), `import sys`, `file=sys.stdout`.
* Пример 1 теории: `source = "print(1 + 2)"; code = compile(source, "<строка>", "exec"); print(type(code).__name__); exec(code)`. Для того, кто ещё не видел переменную, это `compile`/`exec` и объект кода.
* Опровергнуто частично: упражнение «Три строки вывода» лёгкое (три `print`, третья строка `print("6 * 7 =", 6 * 7)`), нет JIT, нет чтения ввода, нет переполнения. Но одна из строк вывода — `Source -> bytecode -> VM`, то есть терминология CPython навязывается в самом первом задании.
* Родственные находки в курсе подтверждены, §3.1.

### 2.5. Тот же паттерн в остальных *-first-steps — ПОДТВЕРЖДЁН ВЕЗДЕ

Цитаты из objectives (`content/courses/<курс>/lessons/01-*/lesson.yaml`):

| Курс | Что нужно знать или понять до первой работающей программы |
|---|---|
| cpp-first-steps | «препроцессор, компилятор, компоновщик»; «что означают -std=c++23, -pthread, -O2, -Wall -Wextra»; `std::cin`, `std::fixed`, `setprecision`; «переполнение int и выход за границы - неопределённое поведение» |
| java-first-steps | «роли javac, файла .class, JVM, интерпретатора и JIT-компиляторов C1 и C2»; `Scanner`; `printf` с локалью; переполнение |
| kotlin-first-steps | `kotlinc`, jar на JVM; `val`/`var`; `Int`/`Long` и явные преобразования; `readln` и `bufferedReader().readText()`; `Locale.ROOT` |
| php-first-steps | «парсер, опкоды, виртуальная машина Zend; роль OPcache и JIT»; «текст вне тегов <?php ... ?> попадает в вывод»; `STDIN`; `sprintf`; `PHP_INT_MAX + 1` |
| rust-first-steps | «компилируется в машинный код без сборщика мусора»; `let`/`let mut`, `i32`/`i64`, `as`; `read_to_string`, `split_whitespace`; «поведение переполнения зависит от режима сборки» |
| typescript-first-steps | «роли проверки типов (tsc) и выполнения (Node.js)»; «стирание типов и ограничения срезания типов в Node.js»; `strict`; чтение stdin |
| nodejs-first-steps | «роли V8, libuv и API Node.js»; `process.argv`, `process.exitCode`; stdin «как поток порций ... через for await»; «JavaScript-код в Node.js работает в одном потоке» |

* Объём теории урока 1 во всех десяти курсах — 737-1034 слов; первый смысловой раздел всегда про устройство языка или среды.
* Ни в одном нет вводного раздела «синтаксис» (переменные, `if`, `for`, функции, строки, массивы). Подсчёт вхождений в `theory.md` урока 1: «цикл» — go 2, kotlin 1, java 1, javascript 1, php 1, cpp/csharp/rust/typescript 0; «функци» — cpp/csharp/java 0, go 3, javascript 4.
* Уроки 2 тоже не «первые»: C++ (`02-value-semantics-references-and-raii`: конструкторы и деструкторы, `struct Section`, порядок вызова деструкторов, висячие ссылки, при том что классы — тема `cpp-fundamentals/04`); Rust (`02-ownership-moves-and-borrowing`: три правила владения, ошибки `E0382`, `E0499`, `E0502`, `E0106`); PHP (`02-arrays-and-strict-types`: нормализация ключей, ссылочный `foreach`, `isset` vs `??`, `declare(strict_types=1)`, сортировка с сохранением ключей); TypeScript (`02`: дискриминированные объединения, `never`-исчерпываемость, проверка формы `unknown`); Java (`02`: кэш `Integer`, `Double.equals`, `Integer.equals(Long)`); Kotlin (`02`: `?.`, `?:`, smart cast, `!!`).
* **(оценка)** Для программиста, меняющего язык, эти курсы выбраны удачно («ловушки языка»), но `level: 0` / `intro` без пометки аудитории вводит в заблуждение.

### 2.6. Framework-курсы: отражает ли `requires` реальные предпосылки

| Курс | `requires` | Реально нужно | Замечание |
|---|---|---|---|
| react-fundamentals | javascript-fundamentals | JS (замыкания, массивы, модули) | в порядке; первые 3 урока `difficulty: intro` при `level: 2` — внутренняя непоследовательность |
| express-fundamentals | javascript-fundamentals, nodejs-fundamentals | JS + HTTP из Node | в порядке; путь `content/paths/express-developer.yaml` пропускает обязательные для них first-steps (§5.3) |
| spring-boot-fundamentals | java-fundamentals | Java (аннотации, DI) | в порядке; урок 1 сразу `medium` |
| aspnetcore-fundamentals | csharp-fundamentals | C# (`async`/`await` — `prerequisites` урока 1 называют `csharp.fundamentals.async-await`) | в порядке |
| django-fundamentals | python-advanced, databases-sql | Python (декораторы, классы), SQL | самая честная цепочка |
| fastapi-fundamentals | python-advanced | Python + **asyncio** | недоопределено: `lessons/01-why-fastapi-and-asgi/theory.md` содержит `await receive()`, `async def`, `import asyncio`; 4 из 7 теорий используют `async def`; `python-concurrency` (где изучается asyncio) в `requires` нет |
| pytorch-fundamentals | ml-foundations, ml-classical-numpy | numpy, классы `nn.Module` | урок 1 ссылается на `python.numpy.broadcasting`, но `python-numpy` в `requires` нет (транзитивно приходит через `ml-classical-numpy`); допустимо |
| go-ml-gomlx | go-fundamentals, ml-foundations | Go + ML | `ml-foundations` написан на Python и требует `python-fundamentals`; изучающий Go вынужден освоить Python |
| databases-sql, os, networking, security, devops, system-design | python-fundamentals | Python (упражнения исполняются Python-harness) | предпосылка — язык реализации упражнений, а не знание темы: человек из Java/Go-пути, которому нужен SQL, вынужден учить Python; `databases-sql/lessons/06` ссылается на `python.fundamentals.errors-and-files` |

Нет требования знать HTTP или сети для веб-фреймворков: в `spring-boot`, `aspnetcore`, `express`, `django`, `fastapi` первые уроки сами объясняют HTTP (11-14 упоминаний «http» в теории урока 1), пробел закрывается внутри курса. **(оценка)** Достаточно.

---

## 3. Анализ по языковым курсам

### 3.1. python-fundamentals (10 уроков, level 0)

(a) **Допущения.** Концепции, использованные до объяснения: `compile`/`exec`/`type(code).__name__` и `import sys` (урок 1). В уроке 2 пример `a = [1, 2]; b = a; b.append(3)` — список и `append` за шесть уроков до урока 8 `lists`. В теории урока 4 (`conditions`) встречается `def` (2 вхождения), хотя функции — урок 6. В теории урока 10 `class BadLineError(ValueError)` и `def __init__`; классы в курсе не преподаются (тема `python-advanced/05`).

(b) **Скачки.** Урок 2 (`intro`, 30 мин): привязка имени к объекту, алиасинг изменяемых объектов, `==` против `is`, `input()` и преобразование. Урок 3: IEEE 754, `0.1 + 0.2`, банковское округление в `round()`, `//` и `%` для отрицательных. Урок 6 (`medium`, 45 мин): позиционные/именованные/умолчательные/только именованные параметры, изменяемое значение по умолчанию, LEGB, `global`/`nonlocal` — самый плотный урок курса и первый с оценкой `harness` вместо `io`.

(c) **Упражнения.** Все посильны: «Три строки вывода», «Приветствие и возраст», «Деление с остатком», «Классификация треугольника», «Последовательность Коллатца», «Четыре функции», «Инструменты для текста», «Четыре операции над списками», «Частотный словарь слов», «Безопасное чтение и запись». Чрезмерных не найдено.

(d) **Квизы.** Вопросов вне теории не обнаружено (только выборка).

(e) **Пропущено.** Кортежи и распаковка, `import` как отдельная тема (нужен для `sys`, `pathlib` в уроке 10), отладка, `pip`/`venv` (0 вхождений в теориях курса). **(оценка)** Для «Основ» приемлемо.

(f) **Дубли.** `pathlib` (урок 10) ↔ `python-stdlib/04`; функции (06) ↔ `python-advanced/01`; классы (10) ↔ `python-advanced/05`.

(g) **Соответствие уровню.** `level: 0`, `difficulty`: уроки 1-2 `intro`, 3-5 `easy`, 6-10 `medium`. Подходит новичку. Первый урок перегружен CPython (байткод, VM, stdout/stderr), упражнения мягкие. **Вердикт: пригоден для абсолютного новичка** (единственный в каталоге).

### 3.2. python-advanced, python-stdlib, python-numpy, python-pandas, python-automation, python-concurrency

* `python-advanced` (level 2): `01` medium → `02-decorators` hard (75 мин) → `03-iterators-and-generators` hard (80 мин) → `04` medium. Два `hard` подряд в начале курса. `08-typing-fundamentals` hard — в конце, это разумно. Скачок с `python-fundamentals/10` (medium) до `02` (hard) заметный; для его смягчения создан placement (§6).
* `python-stdlib` (level 3, `requires: [python-fundamentals]`): `03-itertools-functools-operator`, `04-pathlib-os-shutil-tempfile`, `08-subprocess-and-logging` ссылаются в `prerequisites` на `python.advanced.iterators-and-generators`, `python.advanced.functions-as-objects`, `python.advanced.context-managers`. На уровне уроков `python-advanced` обязателен, на уровне курса нет (major, §5.2).
* `python-automation` (level 3, `requires: [python-stdlib]`): уроки 02-06 ссылаются на `python.advanced.*` (`iterators-and-generators`, `functions-as-objects`, `context-managers`, `dunder-methods-and-dataclasses`, `inheritance-composition-protocols`). 4 урока из 6 `hard` (`03`-`06`, 100-110 мин). Скачок `02` (medium) → `03-robust-http-clients` (hard).
* `python-concurrency` (level 4): `02-threading-and-synchronization` требует `python.stdlib.collections`, а `python-stdlib` в `requires` курса нет. `07` — единственный `expert` в каталоге.
* `python-numpy`/`python-pandas`: `requires` согласованы, цепочки прозрачны.
* Дубли: `algorithms-foundations/02-binary-search` ↔ `python-stdlib/02-heapq-and-bisect` (`bisect`); `python-stdlib/05-json-csv-struct` ↔ `python-automation/02-batch-processing-csv-reports` (CSV); `python-stdlib/08-subprocess-and-logging` ↔ `python-automation/05-logging-config-and-secrets` (logging). **(оценка)** Дубли частичные, глубина разная, но нужны явные отсылки.

### 3.3. go-first-steps и go-fundamentals

(a) См. §2.1.
(b) Скачки: `go-first-steps/03` (intro, 45 мин, `command`) → `go-fundamentals/01` (easy, 55 мин: нетипизированные константы, UTF-8 руны) — курс меняет характер без разрыва по сложности. Внутри `go-fundamentals`: `04-interfaces` (medium) → `05` (medium, 70) → `06-goroutines-and-channels` (medium, 75) → `07-concurrency-patterns-and-context` (hard, 90).
(c) Чрезмерные упражнения: `go-first-steps/01`, `go-first-steps/03` (§2.1). Уроки 04-08 `go-fundamentals` оцениваются `command`, то есть работа с модулем и `go test` нужна с урока 04, а тема «пакеты, модули и тестирование» стоит последней (08). **(оценка)** Порядок можно улучшить.
(e) **Пропущено:** generics (`grep` «обобщ|generic|constraints|cmp.Ordered» по `go-fundamentals/lessons/*/theory.md` — 0 совпадений); `os`, `bufio.Scanner`, `encoding/json`, `time` как темы; `net/http`.
(f) **Дубли** (по objectives):
* `go-first-steps/02-slices-len-cap-and-append` ↔ `go-fundamentals/05-maps-and-slices-deep-dive`: оба про `append`, общий массив, `copy`; у `05` — «Предсказывать влияние append, copy, slices.Clone, slices.Clip и s[a:b:b] на общий массив».
* `go-first-steps/03-packages-and-go-test` ↔ `go-fundamentals/08-packages-modules-testing` (пакеты, `go.mod`, `t.Run`) и ↔ `go-fundamentals/02-functions-and-errors` (`%w`, `errors.Is`).
* `go-first-steps/01` ↔ `go-fundamentals/01` (типы, переполнение, `%q`).
(g) `level: 0`/`intro` не соответствует. **Вердикты:** `go-first-steps` — пригоден для программиста, меняющего язык; `go-fundamentals` — промежуточный.

### 3.4. cpp-first-steps и cpp-fundamentals

* `cpp-first-steps/01` (intro): три этапа сборки, флаги `-std=c++23 -pthread -O2 -Wall -Wextra`, `std::cin`, `std::fixed`/`setprecision`, неопределённое поведение. Упражнение то же «Статистика».
* `cpp-first-steps/02` (intro): упражнение «Копия, ссылка и Section» — RAII-`struct Section` с конструктором и деструктором, порядок вывода `begin sorted`/`end sorted`, числа до 10¹⁰. Классы вводятся без отдельной темы.
* Дубли: RAII `cpp-first-steps/02` ↔ `cpp-fundamentals/03-raii-smart-pointers`; сборка (препроцессор, компилятор, компоновщик) `cpp-first-steps/01` ↔ `cpp-fundamentals/01`.
* Сложность: в `cpp-fundamentals` нет ни одного `easy`-урока (6 `medium` + 2 `hard`, 60-90 мин). `01` (ODR, продолжительность хранения) → `02` (категории значений, move) → `03` → `04-classes-inheritance-virtual`: классы идут после RAII и умных указателей. `05-templates-concepts-constexpr` и `08-undefined-behaviour` — `hard`.
* Пропущено: строки (`std::string_view` упоминается в 3 теориях, отдельного урока нет); потоки (`-pthread` назван в первом уроке, темы нет); организация проекта (CMake/модули).
* **Вердикты:** `cpp-first-steps` — пригоден для программиста, меняющего язык; `cpp-fundamentals` — продвинутый (**оценка**).

### 3.5. csharp-first-steps и csharp-fundamentals

* Дубли: `csharp-first-steps/02-value-types-and-nullable` ↔ `csharp-fundamentals/01-type-system-and-memory` (значимые/ссылочные, `int?` против `string?`) и `02-classes-records-structs` (`struct` vs `class`).
* `csharp-fundamentals/01` помечен `easy`, но содержит `ref`/`out`/`in`, `Span<T>`/`ReadOnlySpan<T>`, упаковку и распаковку; упражнение «Разбор чисел: out, ref и срез Span». **(оценка)** Это `medium`+.
* `csharp-fundamentals/02` тоже `easy` (классы, записи, структуры).
* Пропущено: файловый ввод-вывод (`File.`/`System.IO` — 0 вхождений в теориях), строки и `StringBuilder`, даты, проекты и сборки.
* **Вердикты:** `csharp-first-steps` — пригоден для программиста, меняющего язык; `csharp-fundamentals` — промежуточный.

### 3.6. java-first-steps и java-fundamentals

* `java-first-steps/01`: javac, `.class`, JVM, интерпретатор, JIT C1/C2 — до `Scanner`; упражнение то же. `02`: «Равенство объектов» — парсит строки `тип1 значение1 тип2 значение2` в `Integer`/`Long`/`Double`/`String`, сравнивает `equals` (`int 5 long 5` → `false`).
* Дубли: `java-fundamentals/01-jvm-types-and-memory` начинается с «Описать этапы: javac, байткод, загрузка и проверка класса, интерпретация и JIT-компиляция» — тот же набор тем, что `java-first-steps/01`. Передача параметров по значению — `java-first-steps/02` и `java-fundamentals/01`.
* `java-fundamentals/01` (`easy`): размеры примитивов, сужающие приведения, стек/куча/область методов, `StackOverflowError`/`OutOfMemoryError`, достижимость и GC. **(оценка)** Не `easy`.
* Порядок: `07-threads-executors-virtual` (hard) → `08-text-and-io` (medium); `Scanner` и `printf` используются с урока 1 first-steps, а «Текст и ввод-вывод» учится после потоков.
* Пропущено: `java.time` (0 вхождений), Maven/Gradle (нужны перед Spring Boot; **оценка**, `spring-boot/01` не проверялся на их введение).
* **Вердикты:** `java-first-steps` — пригоден для программиста, меняющего язык; `java-fundamentals` — промежуточный.

### 3.7. kotlin-first-steps и kotlin-fundamentals

* Самые короткие first-steps (737 и 792 слов теории). Урок 1 — `kotlinc`, JVM и то же упражнение «Статистика».
* Дубль: `kotlin-first-steps/02-null-safety-and-val-var` ↔ `kotlin-fundamentals/01-types-and-null-safety`: `val`/`var`, `?.`, `?:`, `!!` разбираются дважды (objectives `kotlin-fundamentals/01`: «Выбирать val или var», «Заменять !! и as на ?:, as? и ?.let»).
* Скачок: `04-collections-and-sequences` (medium) → `05-generics-and-variance` (hard: вариантность, reified) → `06-error-handling` (medium) → `07-concurrency-threads-and-coroutines` (hard, 55 мин). **(оценка)** 55 минут на потоки, синхронизацию и корутины мало.
* **Вердикты:** `kotlin-first-steps` — пригоден для программиста, меняющего язык; `kotlin-fundamentals` — промежуточный.

### 3.8. javascript-first-steps и javascript-fundamentals

* Первые шаги: §2.2. `level: 0`, тогда как `nodejs-first-steps` и `typescript-first-steps` того же объёма — `level: 1`.
* Дубли: `javascript-first-steps/02-values-and-equality` ↔ `javascript-fundamentals/01-types-and-coercion` (приведение типов, `NaN`, `-0`, `Object.is`, `==`). Цикл событий: `javascript-fundamentals/07-promises-and-async` ↔ `nodejs-first-steps/02-modules-and-event-loop` ↔ `nodejs-fundamentals/01-event-loop-architecture` (упоминания «цикл событий» в теориях: 3, 2, 4). `node:test`: `javascript-fundamentals/08-modules-and-testing` ↔ `nodejs-first-steps/03-modules-and-node-test`. ESM/CJS: те же два плюс `nodejs-fundamentals/02-modules-esm-cjs`.
* Сложность: `01` помечен `easy`, но включает `ToPrimitive`, порядок `valueOf`/`toString`, `BigInt`, `MAX_SAFE_INTEGER`. `06-errors-and-exceptions` (medium, 50) → `07-promises-and-async` (hard, 75): промисы и `async`/`await` сразу после ошибок.
* Пропущено: регулярные выражения (2 упоминания), браузер/DOM (в описании трека заявлено как запланированное), итераторы и генераторы в глубину (в плане).
* **Вердикты:** `javascript-first-steps` — пригоден для программиста, меняющего язык; `javascript-fundamentals` — промежуточный.

### 3.9. typescript-first-steps и typescript-fundamentals

* `typescript-first-steps`: `level: 1` (остальные first-steps — 0), `requires: javascript-first-steps`, `prerequisites` урока 1 — `javascript.first-steps.how-javascript-runs`.
* Дубль: `typescript-first-steps/02-structural-typing-and-narrowing` (структурная совместимость, дискриминированные объединения, `never`, `any`/`unknown`) повторяется в `typescript-fundamentals` 01, 02 (`unions-and-narrowing`), 03 (`any-unknown-never`).
* Упражнение урока 1 («Статистика по числам в типизированном коде») повторяет JS-упражнение.
* `typescript-fundamentals` (level 2): оценка `command` (`tsc --strict` над кодом ученика) — сильная сторона, проверяет сами типы. `05-generics` (medium) → `06-utility-mapped-conditional` (hard, 80 мин) — нормальный рост.
* **Вердикты:** `typescript-first-steps` — пригоден для программиста, меняющего язык (знающего JS); `typescript-fundamentals` — промежуточный.

### 3.10. nodejs-first-steps и nodejs-fundamentals

* `nodejs-first-steps`: `level: 1`, `intro`. Урок 1 — `V8`/`libuv`/`process`/чтение stdin через `for await`; урок 2 (intro, 40 мин) — модули ESM/CJS и фазы цикла событий; урок 3 — `node:test`. Упражнения: «Мини-wc: строки, слова и байты», «Суммы по именам в ES-модуле», «Корзина покупок из двух модулей».
* Дубли: цикл событий разбирается трижды, ESM/CJS трижды, `node:test` дважды, V8/libuv трижды (§3.8, §4).
* `nodejs-fundamentals`: `01-event-loop-architecture` ссылается на `nodejs.first-steps.modules-and-event-loop` и `javascript.fundamentals.promises-and-async`; `requires` это отражает. `03-async-patterns` (medium) → `04-streams-backpressure` (hard, 75) → `05-buffers-encodings` (medium); `08-http-fundamentals` hard 90 мин.
* Пропущено: npm и `package.json` (в описании трека как запланированное), `child_process`, `worker_threads`.
* **Вердикты:** `nodejs-first-steps` — промежуточный (нужен JS, реально выше `intro`); `nodejs-fundamentals` — промежуточный.

### 3.11. php-first-steps и php-fundamentals

* `php-first-steps/01`: Zend Engine, опкоды, OPcache, JIT; «текст вне тегов попадает в вывод»; `STDIN`; `PHP_INT_MAX + 1`. `02`: «Частоты слов» с сортировкой по убыванию и порядком по ключам; `declare(strict_types=1)`.
* Дубли: `php-first-steps/02` («Массивы как упорядоченные словари...») ↔ `php-fundamentals/03-arrays-and-functions` («Массивы как упорядоченные хеш-таблицы...»); `strict_types` ↔ `php-fundamentals/02-types-and-juggling`.
* `php-fundamentals/01` помечен `easy`: жизненный цикл запроса, zval, счётчик ссылок, copy-on-write, ссылка `&`, ловушки `foreach` по ссылке; упражнение «Хранилище с вложенными транзакциями». **(оценка)** `medium`+.
* Пропущено: Composer как инструмент (в `07` только автозагрузка), HTTP-запрос и суперглобальные массивы, сессии.
* **Вердикты:** `php-first-steps` — пригоден для программиста, меняющего язык; `php-fundamentals` — промежуточный.

### 3.12. rust-first-steps и rust-fundamentals

* `rust-first-steps`: `level: 1` (остальные 0). Урок 1: «компилируется в машинный код без сборщика мусора», `let mut`, `i32`/`i64`, `as`, `read_to_string`, `split_whitespace`. Урок 2 (`intro`, 45 мин): «Сформулировать три правила владения»; «Читать ошибки E0382, E0499, E0502, E0106 и исправлять их» — одна из самых трудных тем языка внутри вводного курса.
* Дубль: `rust-fundamentals/01-ownership-and-moves-in-depth` и `02-borrowing-and-lifetimes` повторяют урок 2 первых шагов. `01` помечен `easy`, но содержит порядок `drop`, частичное перемещение, `mem::take`/`mem::replace`, `FnOnce`. **(оценка)** `hard`.
* Сложность не монотонна: `02` (medium) → `03-structs-enums-pattern-matching` (easy) → `04` medium → `05-traits-and-generics` (medium, 80 мин, 1969 слов — самая длинная теория среди языковых курсов) → `07-smart-pointers-and-interior-mutability` (hard) → `08-concurrency-fearless` (hard, 90).
* Пропущено: модули и `cargo` (в `rust-fundamentals` `cargo` не встречается; в `rust-first-steps/01` один абзац «Cargo и rustc»), тесты `#[test]` (не проверялось отдельно).
* **Вердикты:** `rust-first-steps` — пригоден для программиста, меняющего язык (урок 2 сложен и для них); `rust-fundamentals` — продвинутый (**оценка**).

### 3.13. Прочие курсы (кратко)

* `algorithms-foundations` (level 1, без `requires`): «упражнения можно решать на Python, JavaScript, Go, Java, C++ или Rust»; уроки 03-06 все ссылаются в `prerequisites` на 01 (`algorithms.foundations.complexity`), не на предыдущий урок. «В каждой задаче большие скрытые тесты отсеивают решения с лишней сложностью» (из `description`). Новичку не подходит; в `requires` ни один языковой курс не указан (§5.4).
* `databases-sql` (level 3): урок 1 `easy`, затем `medium`, `05`-`06` `hard` (100 и 110 мин). Level 3 для вводного SQL выглядит завышенным; курс закрыт Python-предпосылкой.
* `os-fundamentals`, `networking-fundamentals`, `security-fundamentals`, `devops-fundamentals`, `system-design-fundamentals`: level 3-4, `requires: python-fundamentals`. В `os-fundamentals` уроки 03, 05, 07, 08 ссылаются на `01-processes-and-system-calls` (веер, не цепочка). Тяжёлые уроки: `networking/05-tcp-connections-and-congestion` (hard, 120 мин), `security/03-message-authentication-and-tokens` (hard, 120 мин), `devops/07`, `08` (hard, 120 и 110). **(оценка)** Предпосылка `python-fundamentals` для таких тем скорее занижена.
* `ml-foundations`, `ml-classical-numpy`, `pytorch-fundamentals`, `go-ml-gomlx`: цепочки согласованы. **(оценка)** Математика (`ml-math`) в каталоге есть как planned, но курсов нет; `ml-foundations` ждёт от ученика готовность к градиентному спуску и MSE «с нуля».

---

## 4. Дублирование между курсами (сводная таблица)

| Тема | Где повторяется | Оценка |
|---|---|---|
| Срезы / общий массив / `append` / `copy` | `go-first-steps/02` ↔ `go-fundamentals/05` | Дубль выраженный |
| Пакеты, `go.mod`, `t.Run` | `go-first-steps/03` ↔ `go-fundamentals/08` | Дубль выраженный |
| Ошибки-значения, `%w`, `errors.Is` | `go-first-steps/03` ↔ `go-fundamentals/02` | Дубль |
| Устройство среды исполнения (javac/JIT, IL/CLR, Zend, компиляция) | `<lang>-first-steps/01` ↔ `<lang>-fundamentals/01` (java, cpp, csharp, php) | Дубль для java/cpp/csharp/php |
| Null-безопасность `?`, `!!`, `?.` | `kotlin-first-steps/02` ↔ `kotlin-fundamentals/01` | Дубль |
| Значимые/ссылочные типы, `Nullable<T>` | `csharp-first-steps/02` ↔ `csharp-fundamentals/01`, `02` | Дубль |
| Владение и заимствование | `rust-first-steps/02` ↔ `rust-fundamentals/01`, `02` | «Введение» и «глубина» |
| RAII | `cpp-first-steps/02` ↔ `cpp-fundamentals/03` | Дубль |
| Структурные типы, сужение, `unknown`/`never` | `typescript-first-steps/02` ↔ `typescript-fundamentals/01`-`03` | Дубль |
| Равенство, приведение типов | `javascript-first-steps/02` ↔ `javascript-fundamentals/01` | Дубль |
| `==` против `equals`, кэш `Integer` | `java-first-steps/02` ↔ `java-fundamentals/01` | Частично |
| Массивы PHP, `strict_types` | `php-first-steps/02` ↔ `php-fundamentals/02`, `03` | Дубль |
| Цикл событий, ESM/CJS, `node:test`, V8/libuv | `javascript-first-steps/01`, `nodejs-first-steps/01-03`, `javascript-fundamentals/07-08`, `nodejs-fundamentals/01-02` | Тройной дубль |
| Функции/классы Python | `python-fundamentals/06`, `10` ↔ `python-advanced/01`, `05` | Нормальное повторение с углублением |
| `pathlib`, `json/csv`, `logging` | `python-fundamentals/10`, `python-stdlib/04,05,08`, `python-automation/02,05` | Допустимо; нужны отсылки |
| Бинарный поиск / `bisect` | `algorithms-foundations/02` ↔ `python-stdlib/02` | Допустимо |

Общий вывод **(оценка)**: first-steps и fundamentals одного языка устроены как «обзор» и «углубление» одной траектории, поэтому темы объясняются дважды. Для прошедшего first-steps пересказ в fundamentals — потеря времени; для пришедшего сразу в fundamentals нет входной проверки (§6).

---

## 5. Анализ зависимостей (`prerequisites` и `requires`)

### 5.1. Цепочки внутри курсов

Скрипт проверил для каждого урока: id разрешается; предпосылка из того же курса указывает на более ранний урок; перечислены межкурсовые ссылки.

* Висячих ссылок и ссылок «вперёд» внутри курса нет. Циклов нет.
* Во всех линейных курсах (go, cpp, csharp, java, kotlin, php, rust, javascript, typescript, nodejs, python-fundamentals) каждый урок, кроме первого, ссылается на предыдущий.
* Уроков без предпосылок на уровне 0 (`python-fundamentals` и все `*-first-steps`), кроме первого, нет.
* Намеренные «веера» (урок зависит не от предыдущего, а от более раннего): `algorithms-foundations` 03-06 → 01; `ml-foundations` 03, 04 → 01; `os-fundamentals` 03, 05, 07, 08 → 01 (08 ещё и 03); `security-fundamentals` 04 → 01, 08 → `password-storage` и `sessions-csrf-and-cookies`; `system-design-fundamentals` 05 → 03; `express-fundamentals` 06 → `error-handling`; `python-advanced` 03, 05, 07, 08; `python-pandas` 03, 06; `python-stdlib` 03, 04, 07, 08; `ml-classical-numpy` 03, 04; `python-concurrency` 04; `django-fundamentals` 03; `databases-sql` 06. **(оценка)** Подозрительных нет.
* Мелкая нестыковка: `python-fundamentals` 08 (`lists`) ссылается на `loops` и `functions`, но не на `strings`; урок 10 — на `functions` и `strings`, но не на `lists` и `dicts-and-sets`, хотя упражнение работает с коллекциями.

### 5.2. Межкурсовые зависимости

* Из лёгкого курса в более сложный **ссылок нет**: все межкурсовые `prerequisites` и `requires` указывают на курс того же или меньшего `level` (по таблице §1.2). `go-ml-gomlx` (3) → `ml-foundations` (2) → `python-fundamentals` (0): Go-курс опирается на Python-курс.
* Горизонтальные (одинаковый уровень): `nodejs-fundamentals` (1) ← `javascript-fundamentals` (1); `python-pandas` (3) ← `python-numpy` (3); `python-automation` (3) ← `python-stdlib` (3).
* **Нестыковки `requires` курса и `prerequisites` уроков:**
  * `python-stdlib`: `requires: [python-fundamentals]`, уроки 03, 04, 08 требуют `python.advanced.*`.
  * `python-automation`: `requires: [python-stdlib]`, уроки 02-06 требуют `python.advanced.*`.
  * `python-concurrency`: `requires: [python-advanced]`, урок 02 требует `python.stdlib.collections`.
  * `fastapi-fundamentals`: `python-concurrency` не в `requires`, хотя теории используют `async def`/`asyncio`.
  * `pytorch-fundamentals`: `python-numpy` не в `requires`, хотя урок 1 ссылается на `python.numpy.broadcasting`.
  * `go-ml-gomlx`, `databases-sql`, CS-курсы: Python-предпосылка (§2.6).

### 5.3. Пути (`content/paths/`)

* `express-developer.yaml`: stages `javascript-fundamentals` → `nodejs-fundamentals` → `express-fundamentals`; но `javascript-fundamentals` `requires: javascript-first-steps`, а `nodejs-fundamentals` — `nodejs-first-steps`. Эти курсы в пути не перечислены. **(оценка)** Либо путь неполон, либо транзитивные `requires` должны подтягиваться автоматически; поведение приложения не проверялось.
* `react-developer.yaml`, `javascript-developer.yaml`, `typescript-developer.yaml`, `nodejs-developer.yaml` начинаются с `javascript-first-steps`: согласовано.
* `go-backend-developer.yaml` (цель `backend`): только `go-first-steps` и `go-fundamentals`, HTTP и сети нет. **(оценка)** Название шире содержания.
* Пути `python-from-zero`, `python-core`, `python-stdlib`, `nodejs-backend-intro`, `typescript-intro`, `algorithms-interview-prep` описаны через `targets` без `stages`.

### 5.4. Курсы без `requires`, но с неявной предпосылкой

* `algorithms-foundations` (level 1): рассчитан на человека, который уже пишет код на одном из шести языков, но формально открыт любому.
* Все `*-first-steps`, кроме ts/node, без `requires`: открыты любому, реально требуют опыта программирования.

---

## 6. Placement и оценивание

### 6.1. Единственный placement-урок

`content/courses/python-advanced/lessons/00-placement/lesson.yaml` (`id: python.advanced.placement`, `difficulty: medium`, 15 мин, 9 вопросов, `passThreshold: 0.8`, «не менее 8 из 9»).

`assessmentFor` (засчитывает 5 уроков):
* `python.fundamentals.how-python-runs`
* `python.fundamentals.variables-and-input`
* `python.fundamentals.numbers-and-operators`
* `python.fundamentals.conditions`
* `python.fundamentals.loops`

Свойства:
* **Только квиз** (single/multiple/short), без кода. По `docs/content-authoring/README.md` («Make it theory-only with conceptual questions») и `ARCHITECTURE.md` так задумано.
* **Доказательство навыка слабое (оценка):** за 15 минут и 9 вопросов с выбором ответа засчитываются 5 уроков, четыре из которых проверяются кодом (`evaluation: io`: «Приветствие и возраст», «Деление с остатком», «Классификация треугольника», «Последовательность Коллатца»). Вопросы вида «Что выведет код?» проверяют чтение кода, не его написание.
* Не покрывает уроки 06-10 (`functions`, `strings`, `lists`, `dicts-and-sets`, `errors-and-files`). Первый урок `python-advanced` требует `python.fundamentals.functions` (06), которую placement не засчитывает, так что после placement остаётся пройти самый плотный урок `functions`. Сам placement не стоит в `lessons:` курса и не входит в цепочку `prerequisites` урока 01.
* Placement-уроков у остальных курсов нет, `assessmentFor` в других 267 уроках отсутствует. У `*-fundamentals` нет входной проверки: программисту, пришедшему сразу в «язык и идиомы», нечем подтвердить пропуск first-steps.

### 6.2. Что считается доказательством навыка в обычных уроках

* Оценка по коду: `io` (stdout), `harness` (Python-тесты), `command` (например `go test`, `tsc --strict`). В первых шагах языковых курсов в основном `io` (кроме `go-first-steps/03` и `nodejs-first-steps/03` — `command`); в Python-курсах и ML — `harness`.
* В каждом уроке есть квиз (4-10 вопросов); он дополняет упражнение, а не заменяет — хорошая модель. Проблемы квизов (выборка, около 30 вопросов первых уроков, **оценка**):
  * вопросы на припоминание идентификатора: `csharp-first-steps/01` («Какой метод класса Console читает весь стандартный ввод до конца?»), `go-first-steps/01` («Какую функцию использовать с bufio.Reader...» → `fmt.Fscan`), `rust-first-steps/01` («Каким методом строки разбить ввод по пробелам...»), `java-first-steps/01` («Каким методом Scanner читать следующее целое число типа long?»), `php-first-steps/01` («Какая константа даёт доступ к стандартному вводу...»);
  * первый вопрос `javascript-first-steps/01` проверяет устройство V8, а не умение программировать;
  * часть проверяемого материала (например, `Printf` без локали в `go-first-steps/01`) находится в `theory.md` только в секции `precise`.
* Вопросов о теме, которой нет в курсе, в проверенной выборке **не найдено**. Полная сверка всех квизов не проводилась.

---

## 7. Итоговые находки (по убыванию важности)

### 7.1. Критичные (блокируют заявленную аудиторию)

1. **Нет курса «с нуля» ни для одного языка, кроме Python.** `go/cpp/csharp/java/javascript/kotlin/php/rust-first-steps` с `level: 0`/`difficulty: intro` предполагают опыт программирования; вводного синтаксиса (переменные, условия, циклы, функции, коллекции) нет. Файлы: `content/courses/*-first-steps/course.yaml`, `lessons/01-*/lesson.yaml`; пути `content/paths/{go-backend-developer,java-developer,cpp-developer,dotnet-developer,kotlin-developer,php-developer,rust-developer,javascript-developer}.yaml` подают их как первую ступень.
2. **Урок 1 в 9 first-steps курсах — одно и то же перегруженное упражнение «Статистика по числам из stdin»** (sum/min/max/avg, `|x| ≤ 2·10⁹`, int64/long, `%.2f`, скрытые тесты `negatives`, `whitespace`, `rounding`, `big-sum`), вместе с теорией об устройстве компилятора или VM (IL/CLR/JIT, V8/Ignition/TurboFan, Zend/опкоды, javac/C1/C2, препроцессор/компоновщик). Примеры: `go-first-steps/lessons/01-how-go-runs`, `javascript-first-steps/lessons/01-how-javascript-runs`, `csharp-first-steps/lessons/01-how-csharp-runs`.
3. **`go-first-steps/03` использует карту и `%w`/`errors.Is`/табличные тесты до того, как они преподаются** (`go-fundamentals/02`, `05`, `08`); `go-first-steps/02` разбирает внутреннее устройство срезов, которое `go-fundamentals/05` разбирает повторно.
4. **`level: 0` означает «первый запуск» (`CURRICULUM.md` §2), но каталог и пути подают это как «для новичка».** В `course.yaml` и каталоге нет поля «ожидаемая аудитория/предыдущий опыт».

### 7.2. Существенные

5. **Тяжёлый материал в уроках `easy`/`intro`:** `csharp-fundamentals/01` (`ref/out/in`, `Span<T>`), `java-fundamentals/01` (GC, размеры типов, `StackOverflowError`), `rust-fundamentals/01` (`mem::take`, `FnOnce`, порядок `drop`), `php-fundamentals/01` (zval, copy-on-write), `javascript-fundamentals/01` (`ToPrimitive`, `BigInt`), `rust-first-steps/02` (`E0382`, `E0499`, `E0502`, `E0106`), `cpp-first-steps/02` (RAII-классы без введения классов).
6. **Дублирование first-steps → fundamentals** (таблица §4) для Go, Kotlin, C#, Rust, TS, C++, Java, PHP, JS и особенно Node (цикл событий, ESM/CJS, `node:test`), при отсутствии входной проверки, позволяющей пропустить повтор.
7. **`requires` курса расходится с `prerequisites` уроков:** `python-stdlib`, `python-automation`, `python-concurrency`, `fastapi-fundamentals` (asyncio), `pytorch-fundamentals`; путь `express-developer.yaml` не содержит `javascript-first-steps` и `nodejs-first-steps`.
8. **Python «Основы» использует ещё не изученные конструкции:** `list`/`append` в уроке 2 (до урока 8), `def` в уроке 4 (до урока 6), `class` в уроке 10 (классы не преподаются), `compile`/`exec`/`import sys` в уроке 1.
9. **Python-зависимость у не-Python курсов:** `databases-sql`, `os`, `networking`, `security`, `devops`, `system-design`, `ml-foundations` и (транзитивно) `go-ml-gomlx` требуют `python-fundamentals`. **(оценка)** Допустимо как архитектурное решение (Python-harness), но в описаниях курсов не оговорено.
10. **Пробелы тем:** Go — нет generics, `os`/`bufio.Scanner`/`encoding/json`/`time`; Rust — нет модулей и `cargo` как темы; Java — нет `java.time`; C# — нет файлового ввода-вывода; JS — нет браузера/DOM; Python-Основы — нет `import` как темы.
11. **Скачки сложности:** `python-advanced/02` и `03` (подряд `hard`); `python-automation/02` → `03`; `javascript-fundamentals/06` → `07`; `kotlin-fundamentals/05` (hard) и `07` (hard, 55 мин); `rust-fundamentals` 02 → 03 → 05 → 07 (сложность немонотонна); `java-fundamentals/07` (hard) → `08`.

### 7.3. Мелкие

12. Непоследовательные `level` у first-steps: `rust-first-steps`, `typescript-first-steps`, `nodejs-first-steps` — `1`, остальные — `0` при сопоставимом объёме.
13. Завышенный `level` вводных тем: `databases-sql` — `3` при `easy`-старте; `react-fundamentals` — `2`, а первые три урока `intro`.
14. `algorithms-foundations` без `requires`.
15. Вопросы квизов на припоминание идентификатора API (§6.2).
16. Placement единственный, только квиз, покрывает 5 из 10 уроков «Основ» (без `functions`); для `*-fundamentals`, `python-stdlib`, `python-concurrency` его нет.
17. `python-fundamentals/lessons/08` и `10`: `prerequisites` не включают непосредственно предшествующие уроки, используемые в упражнениях.
18. `javascript-first-steps` смешивает язык и Node (`require("node:fs").readFileSync(0, ...)`), противореча описанию трека.
19. Описания курсов не называют аудиторию (кроме `python-fundamentals`).

---

## 8. Вердикты по курсам

Вердикт основан на реальных требованиях (знания из текста уроков и упражнений), а не на заявленном `level`.

| Курс | Заявлено | Вердикт |
|---|---|---|
| python-fundamentals | L0, intro-medium | пригоден для абсолютного новичка |
| go-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| cpp-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| csharp-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| java-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| javascript-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| kotlin-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| php-first-steps | L0, intro | пригоден для программиста, меняющего язык |
| rust-first-steps | L1, intro | пригоден для программиста, меняющего язык (урок 2 сложен и для них) |
| typescript-first-steps | L1, intro | пригоден для программиста, меняющего язык (нужен JS) |
| nodejs-first-steps | L1, intro | промежуточный (нужен JS, реально выше intro) |
| go-fundamentals | L1 | промежуточный |
| java-fundamentals | L1 | промежуточный |
| csharp-fundamentals | L1 | промежуточный |
| kotlin-fundamentals | L1 | промежуточный |
| php-fundamentals | L1 | промежуточный |
| javascript-fundamentals | L1 | промежуточный |
| typescript-fundamentals | L2 | промежуточный |
| nodejs-fundamentals | L1 | промежуточный |
| cpp-fundamentals | L1 | продвинутый |
| rust-fundamentals | L1 | продвинутый |
| python-advanced | L2 | промежуточный |
| python-stdlib | L3 | промежуточный |
| python-numpy | L3 | промежуточный |
| python-pandas | L3 | промежуточный |
| python-automation | L3 | продвинутый |
| python-concurrency | L4 | продвинутый |
| algorithms-foundations | L1 | промежуточный (нужен язык, §5.4) |
| databases-sql | L3 | промежуточный (по теме вводный; закрыт без Python) |
| os-fundamentals | L3 | промежуточный |
| networking-fundamentals | L3 | промежуточный-продвинутый (оценка) |
| security-fundamentals | L3 | промежуточный-продвинутый (оценка) |
| devops-fundamentals | L3 | промежуточный |
| system-design-fundamentals | L4 | продвинутый |
| react-fundamentals | L2 | промежуточный |
| express-fundamentals | L2 | промежуточный |
| spring-boot-fundamentals | L2 | промежуточный |
| aspnetcore-fundamentals | L2 | промежуточный |
| django-fundamentals | L3 | промежуточный |
| fastapi-fundamentals | L3 | промежуточный (asyncio не обеспечен `requires`) |
| ml-foundations | L2 | промежуточный |
| ml-classical-numpy | L3 | продвинутый |
| pytorch-fundamentals | L4 | продвинутый |
| go-ml-gomlx | L3 | продвинутый |

## 9. Рекомендации (кратко, без изменения файлов)

1. Решить продуктовый вопрос: нужны ли курсы «с нуля» для других языков. Если да — по одному курсу «<язык> с нуля» (переменные, условия, циклы, функции, строки, коллекции) перед `*-first-steps`. Если нет — честно маркировать first-steps как «для знакомых с программированием».
2. Сделать «Статистику по числам» не первым упражнением; в начале дать простое «прочитай число, выведи результат».
3. Вынести или сократить `go-first-steps` уроки 2 и 3 и убрать дубли с `go-fundamentals/05`, `02`, `08`.
4. Добавить поле аудитории/требуемого опыта в `course.yaml` и показывать его в каталоге.
5. Привести `requires` в соответствие с `prerequisites` уроков (`python-stdlib`, `python-automation`, `python-concurrency`, `fastapi-fundamentals`, `pytorch-fundamentals`), поправить путь `express-developer`.
6. Выровнять `level` у `rust/typescript/nodejs-first-steps`.
7. Добавить placement-проверки (с кодом, не только квиз) для `*-fundamentals`, чтобы пропускать повторы first-steps.

---

## 10. Статус после переработки (22.10.2026)

Разделы 1-9 описывают состояние **до** переработки и сохранены как исходные данные.

| Курс | Было | Стало |
|---|---|---|
| `go-first-steps` | 3 урока для программистов (компиляция, bufio, int64, устройство срезов, sentinel errors, табличные тесты) | «Go с нуля»: 29 уроков, одно понятие на урок, упражнения A→D, мини-проект; `concepts` у всех уроков |
| `go-for-programmers` | - | новый: 4 урока, зачитывают уроки 01-28 «Go с нуля» |
| `go-skill-check` | - | новый: 11 проверок (код + квиз), маршрут `content/routes/go.yaml` |
| `go-fundamentals` | 8 уроков, первый зависел от урока с sentinel errors | 9 уроков: + бывший `go.first-steps.packages-and-go-test` (без изменений, `formerIds`); первый урок зависит от `go.first-steps.strings-and-runes` |
| `go-professional` | - | новый, уровень 4: 8 уроков (бывший урок о срезах + аллокации, гонки, модель памяти, отмена, ресурсы, мутационное тестирование, escape analysis) |
| `python-fundamentals` | 10 уроков, списки/`def`/`class` до объяснения, CPython в 1-м уроке | 15 уроков, id сохранены, 5 новых; CPython - в `deep-dive`; `concepts` у всех уроков |
| `python-for-programmers`, `python-skill-check` | - | новые: 4 урока и 8 проверок; маршрут `content/routes/python.yaml` |
| `*-first-steps` других языков | уровень 0 при требованиях программиста | уровень 1, в описании прямо указано, что курс для программистов |
| `python-stdlib`, `python-concurrency` | `requires` не отражали prerequisites уроков | `requires` дополнены |
| `javascript-from-zero`, `javascript-for-programmers`, `javascript-skill-check` | - | новые: 23 урока с нуля (`concepts` у всех), 3 урока для программистов, 9 проверок; маршрут `content/routes/javascript.yaml` |
| `javascript-first-steps`, `javascript-fundamentals` | `javascript-fundamentals` без `requires`: React и Express формально не требовали знания языка | `first-steps` требует `javascript-from-zero`, `fundamentals` требует `first-steps`; содержание не менялось |

Перенос прогресса (`formerIds`, только эквивалентное содержание): `go.first-steps.how-go-runs` → `go.first-steps.number-stream`
(то же упражнение и тесты), `go.first-steps.slices-len-cap-and-append` → `go.pro.slice-internals`,
`go.first-steps.packages-and-go-test` → `go.fundamentals.errors-and-table-tests` (содержание без изменений). Новые уроки
«Go с нуля» ничего не зачитывают по старым записям. В `python-fundamentals` id не менялись; у 9 уроков заменено упражнение
(новый id упражнения `...exercise-v2`): прежний зачёт остаётся, потому что умение то же или уже; старые черновики этих
упражнений не переносятся. Удалены два испытания (`python.fundamentals.loops.challenge`, `lists.challenge`).

Не переработано (честный статус): курсы «с нуля» для C++, C#, Java, Kotlin, PHP, Rust, TypeScript; дубли
first-steps / fundamentals в этих треках; профессиональные треки Python (AsyncIO, профилирование, packaging) кроме
существующих курсов; проверки компетенций для языков, кроме Go, Python и JavaScript. Фреймворковые курсы (React, FastAPI, Spring Boot,
ASP.NET Core) по-прежнему опираются на курсы языков через `requires`.

## 11. Педагогическая проверка (экспертная оценка, не исследование с участниками)

Это проверка автора по чек-листу стандарта, а не пользовательское тестирование. Что проверено автоматически, а что - выборочно
вручную, разделено.

**Автоматически (валидатор, на каждом запуске):**

* «Используется до объяснения»: у всех 29 уроков `go-first-steps` и 15 уроков `python-fundamentals` заполнены `concepts`;
  валидатор не нашёл ни одного понятия, используемого раньше, чем его ввели.
* Цепочка prerequisites в курсах уровня 0 без разрывов; сложность только `intro` / `easy` (эвристика, предупреждений нет).
* Эталоны проходят все тесты, заготовки - нет; запускаемые примеры теории дают объявленный вывод (`--run-solutions`).
* Маршруты: точки входа и уроки-проверки существуют, у проверок есть `assessmentFor` и упражнение, порядок `after` без циклов.

**Выборочно вручную (уроки 01, 07, 12, 14, 22, 29 Go; 01, 08 Python):**

| Вопрос стандарта | Результат |
|---|---|
| Понятна ли цель | да: раздел `simple` начинается с «Зачем это нужно» |
| Все ли термины объяснены | да в просмотренных уроках (`&` в `fmt.Scan` объяснён как «адрес», с пометкой «подробно позже») |
| Можно ли выполнить задание только с изученным | да; эталон урока 22 не использует `cap`, урок 08 Python - без функций и списков |
| Не слишком ли много нового | одно-два понятия на урок по метаданным `introduces` |
| Понятно ли, почему решение не прошло | у каждого теста есть подсказка, что проверяется |
| Связь со следующим уроком | `summary` ссылается на следующий урок |

**Профессиональный курс (`go-professional`):** у каждой задачи реальный инженерный контекст (горячий путь с аллокациями,
сервис с гонкой, пул воркеров с отменой, освобождение ресурсов, тесты против мутантов) и объективная проверка
(`testing.AllocsPerRun`, инварианты под нагрузкой, счёт горутин, фейковые ресурсы, мутанты). Ограничения названы в уроках:
скорость, память и поведение GC не измеряются; race detector в песочнице недоступен (нет cgo).

**Не проверено:** все остальные уроки вручную не перечитывались; качество объяснений для реального новичка может
подтвердить только тестирование с участниками, которого не было.
