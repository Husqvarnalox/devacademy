<!-- section: simple -->
## Контейнер и бины

В Spring вы почти не пишете `new` для своих сервисов. Вместо этого вы описываете классы, а **контейнер** (`ApplicationContext`) создаёт их объекты, связывает между собой и хранит. Объект, которым управляет контейнер, называется **бином** (bean). Связывание называется **внедрением зависимостей** (dependency injection, DI): класс не создаёт то, что ему нужно, а объявляет это в конструкторе, и контейнер подставляет готовые объекты.

```java
@Service
class OrderService {
    private final OrderRepository repository;

    OrderService(OrderRepository repository) {   // единственный конструктор: @Autowired не нужен
        this.repository = repository;
    }
}
```

Класс становится бином, если его пометить **стереотипом**: `@Component` (общий случай), `@Service` (логика), `@Repository` (доступ к данным) или `@RestController` (контроллер). Все они - разновидности `@Component`; различие смысловое (у `@Repository` есть ещё перевод исключений доступа к данным). `@SpringBootApplication` включает **сканирование компонентов**: Spring ищет такие классы в пакете приложения и его подпакетах. Поэтому в упражнениях этого курса приложение лежит в пакете `demo`, а все бины - рядом с ним.

Что даёт такой подход: класс не знает, откуда взялись его зависимости. В тесте можно подставить другую реализацию, а замена одной реализации интерфейса на другую не требует правок у тех, кто интерфейс использует.

<!-- section: precise -->
## Точные правила

**Внедрение через конструктор.** Если у класса единственный конструктор, `@Autowired` на нём не нужен. Поля `final`, невозможность создать объект без зависимостей, простые тесты (`new OrderService(fakeRepository)`) - причины, по которым **конструктор** предпочтительнее внедрения в поле (`@Autowired private Repo repo`): поле нельзя сделать `final`, зависимости скрыты, а вне контейнера объект не создать без рефлексии.

**Несколько реализаций интерфейса.** Если подходят два бина, а нужен один, Spring бросает `NoUniqueBeanDefinitionException` при запуске. Выбрать можно так:

* `@Primary` на одном из бинов делает его выбором по умолчанию;
* `@Qualifier("имя")` на параметре выбирает бин по имени (имя бина - значение стереотипа: `@Component("push")`, а по умолчанию это имя класса с маленькой буквы);
* параметр может быть **коллекцией**: `List<Notifier>` получает все бины типа, а порядок в ней задаёт `@Order` (меньшее число - раньше); `Map<String, Notifier>` - все бины, ключ - имя бина.

Если подходящего бина нет вообще, запуск падает с `NoSuchBeanDefinitionException`. Это хорошо: ошибка связывания находится при старте, а не в середине запроса.

**Области видимости (scope).**

* `singleton` (по умолчанию) - один объект на контейнер. Он общий для всех запросов и потоков, поэтому изменяемое состояние в нём должно быть потокобезопасным.
* `prototype` - новый объект при каждом запросе к контейнеру. Внедрённый в singleton через конструктор prototype-бин создаётся один раз при создании singleton; чтобы получать новый объект при каждом использовании, внедряйте `ObjectProvider<T>` и вызывайте `getObject()`.
* `@RequestScope` - один объект на HTTP-запрос. Singleton-бины получают вместо него прокси, который сам находит объект текущего запроса.

**`@Configuration` и `@Bean`.** Класс из чужой библиотеки нельзя пометить `@Component`. Его регистрируют **методом `@Bean`** в классе с `@Configuration`; имя метода - имя бина, параметры метода внедряются так же, как параметры конструктора. Метод `@Bean`, вызванный из другого метода той же конфигурации, возвращает бин контейнера, а не новый объект - это обеспечивает прокси, который Spring создаёт для `@Configuration` (`proxyBeanMethods = true`, по умолчанию). Если не вызывать методы друг из друга, а брать зависимость параметром, прокси не нужен: `@Configuration(proxyBeanMethods = false)` запускается быстрее.

**Жизненный цикл.** Контейнер создаёт бин (конструктор), внедряет зависимости, вызывает метод с `@PostConstruct` - место для инициализации, которая зависит от внедрённых полей, - а при закрытии контекста вызывает `@PreDestroy`. Конструктор не годится для такой инициализации, если она использует внедрение в поля.

**Циклические зависимости.** Если `A` требует `B`, а `B` требует `A`, создать их нельзя: цикл - это ошибка проекта. Spring Boot по умолчанию запрещает такие ссылки, и запуск падает с `BeanCurrentlyInCreationException` внутри `UnsatisfiedDependencyException`. Разорвите цикл: вынесите общую часть в третий класс или передавайте событие вместо вызова.

<!-- section: examples -->
## Примеры

Стереотипы, `@Primary`, `@Qualifier`, коллекция с `@Order` и две типичные ошибки связывания. Файл самодостаточный, без сети: контекст Spring Boot стартует без веб-слоя, бины перечисляются через `@Import` (вместо сканирования, которое в безымянном пакете не годится):

```java run
import java.util.Arrays;
import java.util.List;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Import;
import org.springframework.context.annotation.Primary;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.stereotype.Repository;
import org.springframework.stereotype.Service;

public class Main {
    interface Notifier {
        String send(String text);
    }

    @Component("email")
    @Order(2)
    static class EmailNotifier implements Notifier {
        public String send(String text) { return "email<" + text + ">"; }
    }

    @Component("sms")
    @Order(1)
    @Primary
    static class SmsNotifier implements Notifier {
        public String send(String text) { return "sms<" + text + ">"; }
    }

    @Component("push")
    @Order(3)
    static class PushNotifier implements Notifier {
        public String send(String text) { return "push<" + text + ">"; }
    }

    @Repository
    static class UserRepository {
        String nameOf(int id) { return id == 1 ? "Ann" : "unknown"; }
    }

    @Service
    static class AlertService {
        private final UserRepository users;
        private final Notifier primary;
        private final Notifier push;
        private final List<Notifier> all;

        // The only constructor: no @Autowired needed. Spring resolves every parameter from the container.
        AlertService(UserRepository users, Notifier primary, @Qualifier("push") Notifier push, List<Notifier> all) {
            this.users = users;
            this.primary = primary;
            this.push = push;
            this.all = all;
        }

        void run(int id) {
            String text = "hello " + users.nameOf(id);
            System.out.println("primary: " + primary.send(text));
            System.out.println("qualified: " + push.send(text));
            System.out.println("all in @Order: " + all.stream().map(n -> n.send(text)).toList());
        }
    }

    // A component without @Primary: asking for a single Notifier is ambiguous.
    @Service
    static class Report {
        Report(Notifier notifier) {}
    }

    @Configuration
    @Import({UserRepository.class, EmailNotifier.class, SmsNotifier.class, PushNotifier.class, AlertService.class})
    static class Config {}

    @Configuration
    @Import({EmailNotifier.class, PushNotifier.class, Report.class})
    static class Ambiguous {}

    @Configuration
    @Import(Report.class)
    static class Missing {}

    static ConfigurableApplicationContext start(Class<?> config) {
        return new SpringApplicationBuilder(config)
                .web(WebApplicationType.NONE)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .run("--logging.level.root=OFF");
    }

    public static void main(String[] args) {
        // @Import lists the classes explicitly; in a normal project @SpringBootApplication scans the package instead.
        try (ConfigurableApplicationContext context = start(Config.class)) {
            String[] names = context.getBeanNamesForType(Notifier.class);
            Arrays.sort(names);
            System.out.println("notifier beans: " + Arrays.toString(names));
            context.getBean(AlertService.class).run(1);
            System.out.println("singleton: " + (context.getBean(AlertService.class) == context.getBean(AlertService.class)));
        }
        for (Class<?> config : List.of(Ambiguous.class, Missing.class)) {
            try (ConfigurableApplicationContext context = start(config)) {
                System.out.println(config.getSimpleName() + ": started");
            } catch (Exception e) {
                System.out.println(config.getSimpleName() + ": " + e.getClass().getSimpleName()
                        + " <- " + e.getCause().getClass().getSimpleName());
            }
        }
    }
}
```
```output
notifier beans: [email, push, sms]
primary: sms<hello Ann>
qualified: push<hello Ann>
all in @Order: [sms<hello Ann>, email<hello Ann>, push<hello Ann>]
singleton: true
Ambiguous: UnsatisfiedDependencyException <- NoUniqueBeanDefinitionException
Missing: UnsatisfiedDependencyException <- NoSuchBeanDefinitionException
```

Параметр `Notifier primary` получил `SmsNotifier` из-за `@Primary`, параметр с `@Qualifier("push")` - бин с именем `push`, а `List<Notifier>` - все три в порядке `@Order`. Оба запуска с ошибками упали при старте контекста: не получилось выбрать из двух бинов (`NoUniqueBeanDefinitionException`) и не нашёлся ни один (`NoSuchBeanDefinitionException`).

Области видимости наглядно: у каждого объекта есть номер, и по нему видно, один это объект или разные. Запрос `/scopes` идёт через MockMvc:

```java run
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicInteger;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.context.annotation.Scope;
import org.springframework.mock.web.MockServletContext;
import org.springframework.stereotype.Component;
import org.springframework.stereotype.Service;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.annotation.RequestScope;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    // Every instance takes the next number from a shared counter, so the number says "which object is this".
    static final AtomicInteger INSTANCES = new AtomicInteger();

    @Component                       // default scope: singleton, one object per container
    static class Shared {
        final int id = INSTANCES.incrementAndGet();
        final AtomicInteger calls = new AtomicInteger();
    }

    @Component
    @Scope("prototype")              // a new object every time the container is asked for one
    static class PerUse {
        final int id = INSTANCES.incrementAndGet();
    }

    @Component
    @RequestScope                    // one object per HTTP request (the singletons below get a proxy)
    static class PerRequest {
        final int id = INSTANCES.incrementAndGet();
        public int id() { return id; }
    }

    @Service
    static class Helper {
        private final PerRequest request;
        Helper(PerRequest request) { this.request = request; }
        int requestId() { return request.id(); }
    }

    @RestController
    static class ScopeController {
        private final Shared shared;
        private final ObjectProvider<PerUse> perUse;
        private final PerRequest request;
        private final Helper helper;

        ScopeController(Shared shared, ObjectProvider<PerUse> perUse, PerRequest request, Helper helper) {
            this.shared = shared;
            this.perUse = perUse;
            this.request = request;
            this.helper = helper;
        }

        @GetMapping("/scopes")
        String scopes() {
            return "shared#" + shared.id + " call " + shared.calls.incrementAndGet()
                    + ", prototype#" + perUse.getObject().id + " and #" + perUse.getObject().id
                    + ", request#" + request.id() + ", helper sees request#" + helper.requestId();
        }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import({Shared.class, PerUse.class, PerRequest.class, Helper.class, ScopeController.class})
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            for (int i = 1; i <= 3; i++) {
                System.out.println("request " + i + ": " + mvc.perform(MockMvcRequestBuilders.get("/scopes"))
                        .andReturn().getResponse().getContentAsString(StandardCharsets.UTF_8));
            }
        }
    }
}
```
```output
request 1: shared#1 call 1, prototype#2 and #3, request#4, helper sees request#4
request 2: shared#1 call 2, prototype#5 and #6, request#7, helper sees request#7
request 3: shared#1 call 3, prototype#8 and #9, request#10, helper sees request#10
```

`Shared` создан один раз и живёт во всех трёх запросах (вызов растёт). Каждый `getObject()` у prototype-бина даёт новый номер. Бин `@RequestScope` один на запрос: контроллер и вспомогательный сервис `Helper` видят **один и тот же** объект, а в следующем запросе номер уже новый. Заметьте, что номера «сквозные»: они выданы общим счётчиком в порядке создания.

Теперь `@Bean`, жизненный цикл и циклические зависимости. `proxyBeanMethods` решает, вернёт ли вызов `engine()` из другого `@Bean`-метода тот же бин:

```java run
import jakarta.annotation.PostConstruct;
import jakarta.annotation.PreDestroy;
import java.util.ArrayList;
import java.util.List;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Import;
import org.springframework.stereotype.Component;

public class Main {
    static final List<String> LOG = new ArrayList<>();
    static int engines;

    // A class "from a library": no annotations on it, so it can only be registered with a @Bean method.
    static class Engine {
        Engine() { engines++; }
    }
    record Car(Engine engine) {}
    record Truck(Engine engine) {}

    @Configuration                                   // proxyBeanMethods = true is the default
    static class Full {
        @Bean Engine engine() { return new Engine(); }
        @Bean Car car() { return new Car(engine()); }       // engine() returns the container's bean
        @Bean Truck truck() { return new Truck(engine()); }
    }

    @Configuration(proxyBeanMethods = false)         // plain calls: every engine() is a new Engine
    static class Lite {
        @Bean Engine engine() { return new Engine(); }
        @Bean Car car() { return new Car(engine()); }
        @Bean Truck truck() { return new Truck(engine()); }
    }

    @Component
    static class Connection {
        Connection() { LOG.add("constructor"); }
        @PostConstruct void open() { LOG.add("@PostConstruct"); }
        @PreDestroy void close() { LOG.add("@PreDestroy"); }
    }

    @Component static class A { A(B b) {} }
    @Component static class B { B(A a) {} }

    @Component static class C { @Autowired D d; }
    @Component static class D { @Autowired C c; }

    static ConfigurableApplicationContext start(Class<?>... sources) {
        return new SpringApplicationBuilder(sources)
                .web(WebApplicationType.NONE)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .run("--logging.level.root=OFF");
    }

    @Configuration @Import({A.class, B.class}) static class ConstructorCycle {}
    @Configuration @Import({C.class, D.class}) static class FieldCycle {}

    public static void main(String[] args) {
        for (Class<?> config : List.of(Full.class, Lite.class)) {
            engines = 0;
            try (ConfigurableApplicationContext context = start(config)) {
                boolean same = context.getBean(Car.class).engine() == context.getBean(Truck.class).engine();
                System.out.println(config.getSimpleName() + ": engines created = " + engines + ", same engine = " + same);
            }
        }

        try (ConfigurableApplicationContext context = start(Connection.class)) {
            LOG.add("context is running");
        }
        System.out.println("lifecycle: " + LOG);

        for (Class<?> config : List.of(ConstructorCycle.class, FieldCycle.class)) {
            try (ConfigurableApplicationContext context = start(config)) {
                System.out.println(config.getSimpleName() + ": started");
            } catch (Exception e) {
                Throwable root = e;
                while (root.getCause() != null) root = root.getCause();
                System.out.println(config.getSimpleName() + ": " + e.getClass().getSimpleName() + " ... " + root.getClass().getSimpleName());
            }
        }
    }
}
```
```output
Full: engines created = 1, same engine = true
Lite: engines created = 3, same engine = false
lifecycle: [constructor, @PostConstruct, context is running, @PreDestroy]
ConstructorCycle: UnsatisfiedDependencyException ... BeanCurrentlyInCreationException
FieldCycle: UnsatisfiedDependencyException ... BeanCurrentlyInCreationException
```

В `Full` вызов `engine()` из `car()` и `truck()` вернул единственный бин, в `Lite` - новые объекты (три: бин `engine` и по одному для `car` и `truck`). Жизненный цикл идёт по порядку: конструктор, `@PostConstruct`, работа, `@PreDestroy` при закрытии. Оба цикла - через конструкторы и через поля - не запустились. Подробнее о том же в ASP.NET Core: [внедрение зависимостей](lesson:csharp.aspnetcore.dependency-injection) (там три времени жизни: Singleton, Scoped, Transient; у Spring им соответствуют singleton, `@RequestScope` и prototype).

<!-- section: mistakes -->
## Типичные ошибки

* **Внедрять зависимости в поля.** `@Autowired private Repo repo` скрывает зависимости, не даёт сделать поле `final` и мешает создать объект в тесте без контейнера. Используйте конструктор.
* **Два бина одного типа без выбора.** Запуск падает с `NoUniqueBeanDefinitionException`. Решение - `@Primary`, `@Qualifier` или параметр-коллекция; не оставляйте «случайный» выбор по имени параметра.
* **Хранить состояние запроса в singleton.** Бин общий для всех потоков: поле `currentUser` в сервисе перепутает пользователей. Состояние запроса держат в `@RequestScope`-бине или передают параметрами.
* **Ждать нового prototype при каждом вызове.** Prototype, внедрённый в singleton конструктором, создаётся один раз. Нужен новый объект на каждое использование - внедряйте `ObjectProvider<T>`.
* **Забыть, что класс вне сканируемого пакета не станет бином.** Компоненты ищутся от пакета класса `@SpringBootApplication`; класс в соседнем пакете останется незамеченным, и если на него есть зависимость, запуск упадёт с `NoSuchBeanDefinitionException`.
* **Решать цикл флагом `spring.main.allow-circular-references`.** Он прячет проблему проекта; разорвите цикл.
* **Ставить `@Component` на чужой класс.** Его нельзя изменить, поэтому регистрируйте его методом `@Bean`.

<!-- section: summary -->
## Итоги

* Контейнер создаёт бины и подставляет зависимости; бином класс делают стереотипы (`@Component`, `@Service`, `@Repository`, `@RestController`) и методы `@Bean`.
* Предпочитайте внедрение через единственный конструктор: `@Autowired` не нужен, поля `final`, тесты просты.
* Несколько реализаций: `@Primary`, `@Qualifier`, `List<T>` с `@Order`, `Map<String, T>`.
* Области: singleton (по умолчанию), prototype (новый при каждом запросе к контейнеру; в singleton - через `ObjectProvider`), `@RequestScope`.
* `@PostConstruct` и `@PreDestroy` ограничивают инициализацию и освобождение ресурсов; циклические зависимости запрещены и устраняются перепроектированием.

Дальше: [конфигурационные свойства и профили](lesson:java.spring.configuration-properties).
