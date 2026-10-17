<!-- section: simple -->
## MockMvc: веб-слой без сети

Чтобы проверить контроллер, не нужно запускать сервер и открывать порт. **MockMvc** - это класс из `spring-test`, который строит фальшивый запрос (`MockHttpServletRequest`), передаёт его настоящему `DispatcherServlet` в том же процессе и возвращает фальшивый ответ (`MockHttpServletResponse`). Маршрутизация, привязка параметров, Jackson, валидация и обработчики ошибок работают по-настоящему; нет только сети и контейнера сервлетов.

Тест выглядит как рассказ: *выполнить запрос - проверить ответ*:

```java
mvc.perform(get("/products/1"))
   .andExpect(status().isOk())
   .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON));
```

`perform` возвращает `ResultActions`; каждый `andExpect(...)` проверяет одну деталь ответа и при несовпадении бросает `AssertionError`. Так же работают все тесты этого курса: в упражнениях вы сами пишете проверки, а проверка курса запускает их на правильном и на неправильных вариантах приложения.

**Граница курса.** В обычном проекте вы подключаете `spring-boot-starter-webmvc-test`, который приносит JUnit и аннотации `@SpringBootTest` и `@AutoConfigureMockMvc` (в Spring Boot 4 она лежит в пакете `org.springframework.boot.webmvc.test.autoconfigure`), и получаете готовый `MockMvc` через `@Autowired`. В набор библиотек курса эти артефакты не входят (песочнице нужны `spring-test`, JsonPath и Hamcrest), поэтому здесь приложение запускается вручную, как в предыдущих уроках: `SpringApplicationBuilder` и `MockMvcBuilders.webAppContextSetup`. Всё остальное - запросы, ожидания, `ResultActions`, `jsonPath(...).value(...)` - работает так же, как в обычном проекте. Фрагмент с JUnit ниже **не запускался** в курсе и приведён только для сравнения.

<!-- section: precise -->
## Точные правила

**Что такое и чем не является.** MockMvc проверяет логику Spring MVC: маршруты, привязку, преобразования, фильтры (если их подключить), обработку исключений. Он не проверяет сетевую часть и поведение контейнера (страницы ошибок сервера, TLS, настоящие потоки запросов). Исключение, которое не обработал ни один обработчик, MockMvc бросает из `perform(...)` вызывающему коду, тогда как настоящий сервер ответил бы 500.

**Два способа построить.**

* `MockMvcBuilders.standaloneSetup(new MyController())` - контекст приложения не нужен: проверяется один контроллер с настройками MVC по умолчанию. Советы подключают через `setControllerAdvice(...)`, конвертеры - через `setMessageConverters(...)`. Быстро, но не воспроизводит вашу конфигурацию.
* `MockMvcBuilders.webAppContextSetup(webApplicationContext)` - настоящий контекст с бинами, советами `@RestControllerAdvice` и автоконфигурацией. Ближе к реальности; в Spring Boot это то же, что делает `@AutoConfigureMockMvc`.

**Фильтры** не применяются сами: их передают в `addFilters(filter1, filter2)` при построении. Spring Boot с `@AutoConfigureMockMvc` добавляет бины-фильтры автоматически; в ручной сборке это ваша работа.

**Запросы.** Статические методы `MockMvcRequestBuilders`: `get(url)`, `post(url)`, `put`, `delete`, `patch`, `request(method, url)`. Настройка: `.param("name", "Anna")`, `.header("X-Source", "test")`, `.contentType(MediaType.APPLICATION_JSON)`, `.accept(...)`, `.content("{...}")`.

**Ожидания** (`MockMvcResultMatchers`): `status().isOk()`, `isCreated()`, `isNotFound()`, `isBadRequest()`; `content().contentType(...)`, `contentTypeCompatibleWith(...)`; `header().exists("Location")`, `header().doesNotExist(...)`; `jsonPath("$.name").exists()`. Для сравнения значений: `status().is(201)`, `header().string("Location", "/products/3")`, `content().string("pong")`, `jsonPath("$.name").value("Tea")`, `jsonPath("$.items").isArray()`, `jsonPath("$.price").doesNotExist()` и `jsonPath("$.items", hasSize(2))` (Hamcrest). Многие из этих методов перегружены: одна версия сравнивает значение напрямую, другая принимает `Matcher` из Hamcrest, поэтому Hamcrest должен быть в classpath, иначе компилятор отвергнет вызов. В наборе библиотек курса он есть.

**Разбор результата.** `andReturn()` возвращает `MvcResult`, из которого берут `getResponse()` (`getStatus()`, `getHeader(...)`, `getContentAsString()`) и `getRequest()`; `andExpectAll(a, b, c)` выполняет все ожидания и сообщает обо всех несовпадениях сразу. `andDo(print())` выводит запрос и ответ в `System.out`: удобно для отладки, но это не проверка, а в журнале тестов шум; в готовых тестах его убирают.

**Как выглядит тест в обычном проекте** (не запускалось в курсе: нет JUnit и `spring-boot-starter-webmvc-test`):

```java
@SpringBootTest
@AutoConfigureMockMvc
class ProductApiTest {
    @Autowired MockMvc mvc;

    @Test
    void returnsProduct() throws Exception {
        mvc.perform(get("/products/1"))
           .andExpect(status().isOk())
           .andExpect(jsonPath("$.name").value("Tea"));
    }
}
```

<!-- section: examples -->
## Примеры

`standaloneSetup`: контекст Spring не создаётся, проверяется один контроллер. Показаны запрос с заголовком и телом, ожидания к статусу, Content-Type и JSON, а также сообщения, которые видит автор теста при несовпадении. Пример самодостаточен: так как автоконфигурации Spring Boot здесь нет, журнал Logback выключен вручную:

```java run
import java.util.List;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

public class Main {
    record Item(int id, String name) {}

    @RestController
    static class Items {
        @GetMapping("/items")
        List<Item> list() { return List.of(new Item(1, "Tea"), new Item(2, "Coffee")); }

        @GetMapping("/items/{id}")
        Item one(@PathVariable int id) { return new Item(id, "Tea"); }

        @PostMapping("/items")
        Item create(@RequestBody Item item, @RequestHeader(name = "X-Source", defaultValue = "-") String source) {
            return new Item(item.id() + 100, item.name() + " via " + source);
        }
    }

    public static void main(String[] args) throws Exception {
        // Without Spring Boot nothing configures logging, so silence Logback to keep the output readable.
        ((ch.qos.logback.classic.Logger) org.slf4j.LoggerFactory.getLogger(org.slf4j.Logger.ROOT_LOGGER_NAME)).setLevel(ch.qos.logback.classic.Level.OFF);
        // standaloneSetup: no Spring context at all, just the controller object and the default MVC configuration.
        MockMvc mvc = MockMvcBuilders.standaloneSetup(new Items()).build();

        mvc.perform(get("/items"))
                .andExpect(status().isOk())
                .andExpect(content().contentType(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.length()").value(2))
                .andExpect(jsonPath("$[0].name").value("Tea"))
                .andExpect(jsonPath("$[1].id").value(2));
        System.out.println("list: all expectations passed");

        mvc.perform(post("/items")
                        .contentType(MediaType.APPLICATION_JSON)
                        .header("X-Source", "test")
                        .content("{\"id\":5,\"name\":\"Cocoa\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(105))
                .andExpect(jsonPath("$.name").value("Cocoa via test"))
                .andExpect(jsonPath("$.price").doesNotExist());
        System.out.println("create: all expectations passed");

        mvc.perform(get("/items/7").accept(MediaType.APPLICATION_XML)).andExpect(status().isNotAcceptable());
        System.out.println("xml: 406 as expected");

        try {
            mvc.perform(get("/items/7")).andExpect(status().isCreated());
        } catch (AssertionError e) {
            System.out.println("failed expectation: " + e.getMessage());
        }
        try {
            mvc.perform(get("/items/7")).andExpect(jsonPath("$.name").value("Coffee"));
        } catch (AssertionError e) {
            System.out.println("failed expectation: " + e.getMessage());
        }
        try {
            mvc.perform(get("/items/7")).andExpect(jsonPath("$.price").value(250));
        } catch (AssertionError e) {
            System.out.println("failed expectation: " + e.getMessage());
        }
    }
}
```
```output
list: all expectations passed
create: all expectations passed
xml: 406 as expected
failed expectation: Status expected:<201> but was:<200>
failed expectation: JSON path "$.name" expected:<Coffee> but was:<Tea>
failed expectation: No value at JSON path "$.price"
```

Обратите внимание на тексты ошибок: `Status expected:<201> but was:<200>` - это сообщение настоящего ожидания `status().isCreated()`, а проверка JSON-поля отличается только именем. По такому сообщению сразу видно, что нарушено.

`webAppContextSetup` на настоящем контексте Spring Boot и фильтр. Один и тот же контекст обслуживает два `MockMvc`: без фильтра и с `addFilters`; затем `andReturn()` и `andExpectAll`:

```java run
import jakarta.servlet.Filter;
import java.nio.charset.StandardCharsets;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

public class Main {
    @RestController
    static class Greeter {
        @GetMapping("/greet")
        String greet(@RequestParam(defaultValue = "world") String name) { return "Hello, " + name + "!"; }

        @PostMapping("/echo")
        String echo(@RequestBody String body) { return body.toUpperCase(); }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import(Greeter.class)
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            WebApplicationContext webContext = (WebApplicationContext) context;

            // A servlet filter: stamps a header on every response.
            Filter trace = (request, response, chain) -> {
                ((jakarta.servlet.http.HttpServletResponse) response).setHeader("X-Trace", "on");
                chain.doFilter(request, response);
            };

            MockMvc plain = MockMvcBuilders.webAppContextSetup(webContext).build();
            MockMvc filtered = MockMvcBuilders.webAppContextSetup(webContext).addFilters(trace).build();

            plain.perform(get("/greet").param("name", "Анна"))
                    .andExpect(status().isOk())
                    .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_PLAIN))
                    .andExpect(content().encoding(StandardCharsets.UTF_8))
                    .andExpect(header().doesNotExist("X-Trace"));
            System.out.println("plain: no X-Trace header, the filter is not part of this MockMvc");

            filtered.perform(get("/greet"))
                    .andExpect(status().isOk())
                    .andExpect(header().exists("X-Trace"));
            System.out.println("filtered: X-Trace is present");

            // andReturn() hands over the raw result for any custom assertion.
            MvcResult result = filtered.perform(post("/echo").contentType(MediaType.TEXT_PLAIN).content("abc"))
                    .andExpect(status().isOk())
                    .andReturn();
            MockHttpServletResponse response = result.getResponse();
            System.out.println("echo: " + response.getStatus() + " " + response.getContentAsString()
                    + " trace=" + response.getHeader("X-Trace"));

            // andExpectAll runs every expectation and reports all failures together.
            try {
                filtered.perform(get("/greet?name=Bob")).andExpectAll(
                        status().isCreated(),
                        content().contentType(MediaType.APPLICATION_JSON),
                        header().doesNotExist("X-Trace"));
            } catch (AssertionError e) {
                System.out.println("andExpectAll failed with " + e.getClass().getSimpleName() + ":");
                System.out.println(e.getMessage().lines().map(line -> "  " + line).reduce((a, b) -> a + "\n" + b).orElse(""));
            }
        }
    }
}
```
```output
plain: no X-Trace header, the filter is not part of this MockMvc
filtered: X-Trace is present
echo: 200 ABC trace=on
andExpectAll failed with AssertionError:
  Multiple Exceptions (3):
  Status expected:<201> but was:<200>
  Content type expected:<application/json> but was:<text/plain;charset=UTF-8>
  Response should not contain header 'X-Trace'
```

Первый `MockMvc` не знает о фильтре: заголовка `X-Trace` нет, и ожидание `doesNotExist` выполняется. Второй получил фильтр через `addFilters`, и заголовок появился. `andExpectAll` собрал в одно сообщение сразу три несовпадения вместо того, чтобы остановиться на первом.

<!-- section: mistakes -->
## Типичные ошибки

* **Тест без ожиданий или с ожиданием, которое выполняется всегда.** Такой тест всегда зелёный. Проверяйте его на неправильном коде: он обязан упасть.
* **Проверять только статус.** Код 200 и пустое тело, код 200 и неверный Content-Type, 201 без `Location` - разные ошибки. Проверяйте код, тип, заголовки и поля тела.
* **Ждать фильтры без `addFilters`.** В ручной сборке MockMvc фильтры сервлетов не участвуют; в тесте с `@AutoConfigureMockMvc` бины-фильтры подключены сами.
* **Принимать `andDo(print())` за проверку.** Он ничего не проверяет и шумит в выводе; пользуйтесь `andExpect`.
* **Использовать `standaloneSetup` и удивляться, что `@RestControllerAdvice` не сработал.** В standalone советы нужно перечислить в `setControllerAdvice(...)`; с `webAppContextSetup` они берутся из контекста.
* **Считать MockMvc проверкой развёртывания.** Он не видит порта, TLS и страниц ошибок контейнера; для этого нужны отдельные проверки на реальном сервере.

<!-- section: summary -->
## Итоги

* MockMvc гоняет запросы через настоящий `DispatcherServlet` без сети и порта; каждое `andExpect(...)` бросает `AssertionError` при несовпадении.
* `standaloneSetup` проверяет один контроллер, `webAppContextSetup` - приложение целиком; фильтры подключаются через `addFilters`.
* Запросы строятся методами `get`, `post`, `param`, `header`, `contentType`, `content`; результат разбирается через `andReturn()` и `andExpectAll`.
* Ожидания пишут стандартными вызовами `status()`, `header()`, `content()`, `jsonPath()`; в обычном проекте тесты запускают через JUnit с `@SpringBootTest` и `@AutoConfigureMockMvc`.
* Хороший тест падает на неправильной реализации: проверяйте его на заведомо неверном коде.

Дальше: [небольшой CRUD-сервис](lesson:java.spring.crud-service).
