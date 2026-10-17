<!-- section: simple -->
## Spring Boot: приложение из контроллеров

**Spring Boot** собирает веб-приложение на Java вокруг одной идеи: вы описываете классы-**контроллеры**, а фреймворк сам разбирает запрос, находит нужный метод, подставляет в него значения из адреса и тела и превращает результат в HTTP-ответ. Контроллер - обычный класс с аннотацией `@RestController`, а методы помечены `@GetMapping`, `@PostMapping` и другими: аннотация задаёт метод HTTP и шаблон пути.

```java
@SpringBootApplication
public class App {
    public static void main(String[] args) {
        SpringApplication.run(App.class, args);
    }

    @RestController
    static class HelloController {
        @GetMapping("/hello/{name}")
        String hello(@PathVariable String name) {
            return "Hello, " + name + "!";
        }
    }
}
```

`@SpringBootApplication` включает сканирование компонентов в своём пакете и автоконфигурацию: Spring Boot видит на пути классов нужные библиотеки и сам настраивает приложение. Метод и шаблон пути выбирают один метод контроллера; если подходящего пути нет, ответ - 404, а если путь есть, но метод HTTP другой, - 405.

В этом курсе приложение никогда не слушает порт: проверка поднимает настоящий контекст Spring Boot **без веб-сервера** и подаёт запросы прямо в `DispatcherServlet` через **MockMvc**. Код контроллеров от этого не меняется, а проверка идёт быстро и без сети.

Что метод возвращает, то и становится ответом: `String` - `text/plain`, объект, запись (`record`) или коллекция - JSON, а `ResponseEntity` позволяет явно задать код состояния и заголовки.

<!-- section: precise -->
## Точные правила

**Отображение запросов.** `@GetMapping`, `@PostMapping`, `@PutMapping`, `@DeleteMapping` и `@RequestMapping(method = ...)` связывают метод HTTP и шаблон пути с методом контроллера. `@RequestMapping` на классе задаёт общий префикс пути для всех его методов. При нескольких подходящих шаблонах побеждает более конкретный: литерал сильнее переменной.

**Шаблоны.** Фигурные скобки задают переменную пути: `/books/{id}`. После двоеточия можно записать регулярное выражение: `{id:\d+}` совпадёт только с цифрами. Конец шаблона `{*path}` захватывает остаток пути вместе с косыми чертами (значение начинается с `/`).

**Привязка параметров.** Источник значения указывает аннотация:

* `@PathVariable` - переменная из пути;
* `@RequestParam` - параметр строки запроса (или поле формы);
* `@RequestHeader` - заголовок запроса;
* `@RequestBody` - тело запроса, которое Jackson превращает в объект.

Параметр `@RequestParam` и `@PathVariable` **обязателен** по умолчанию: без значения Spring отвечает 400. Необязательным его делают `required = false`, `defaultValue = "..."` или `Optional<T>`. Строку, которую нельзя преобразовать в нужный тип (`abc` вместо `int`), Spring тоже отвергает с 400. Имена параметров метода берутся из байткода, поэтому код компилируется с флагом `-parameters` (Maven и Gradle для Spring Boot включают его сами; проверка в этом курсе тоже).

**Результаты.** Метод возвращает `String` (`text/plain`), запись или коллекцию (JSON; имена полей записи становятся именами свойств) либо `ResponseEntity<T>`: `ResponseEntity.ok(body)`, `ResponseEntity.created(uri).body(body)` (201 и заголовок `Location`), `ResponseEntity.notFound().build()`, `ResponseEntity.noContent().build()`.

**Контроллер - один объект на приложение.** Spring создаёт контроллер один раз (singleton), и один и тот же экземпляр обслуживает запросы с разных потоков. Поле-список в контроллере из этого урока подходит только как учебная заготовка; для настоящих данных нужны сервисы (следующие уроки).

<!-- section: examples -->
## Примеры

Шаблоны, разные ответы 404/405/400. Файл самодостаточный: он стартует контекст Spring Boot без веб-сервера и шлёт запросы через MockMvc. Контроллер регистрируется явно (`@Import`), поэтому сканирование пакетов не нужно:

```java run
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.HttpMethod;
import org.springframework.http.ResponseEntity;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    @RestController
    static class Demo {
        @GetMapping("/")
        String root() { return "root"; }

        @GetMapping("/hello/{name}")
        String hello(@PathVariable String name) { return "Hello, " + name + "!"; }

        @GetMapping("/orders/{id}")
        String order(@PathVariable int id) { return "order " + id; }

        @GetMapping("/files/{*path}")
        String file(@PathVariable String path) { return "file " + path; }

        @PostMapping("/orders")
        ResponseEntity<Map<String, Integer>> create() {
            return ResponseEntity.created(URI.create("/orders/7")).body(Map.of("id", 7));
        }
    }

    // No component scan: the controller is registered explicitly, so a single file in the default package is enough.
    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import(Demo.class)
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            String[][] requests = {
                {"GET", "/"}, {"GET", "/hello/Анна"}, {"GET", "/orders/42"}, {"GET", "/orders/abc"},
                {"GET", "/files/a/b/c.txt"}, {"DELETE", "/orders/42"}, {"POST", "/orders"}, {"GET", "/nope"},
            };
            for (String[] request : requests) {
                MockHttpServletResponse response = mvc.perform(
                        MockMvcRequestBuilders.request(HttpMethod.valueOf(request[0]), request[1])).andReturn().getResponse();
                String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
                System.out.println(String.format("%-6s %-18s -> %d %s %s", request[0], request[1], response.getStatus(), type,
                        response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
            }
        }
    }
}
```
```output
GET    /                  -> 200 text/plain root
GET    /hello/Анна        -> 200 text/plain Hello, Анна!
GET    /orders/42         -> 200 text/plain order 42
GET    /orders/abc        -> 400 -
GET    /files/a/b/c.txt   -> 200 text/plain file /a/b/c.txt
DELETE /orders/42         -> 405 -
POST   /orders            -> 201 application/json {"id":7}
GET    /nope              -> 404 -
```

Обратите внимание: `/orders/abc` попал в маршрут, но `abc` нельзя превратить в `int`, поэтому 400 (в ASP.NET Core ограничение маршрута дало бы 404; сравните с [минимальными API](lesson:csharp.aspnetcore.minimal-apis-routing)). А `DELETE /orders/42` попал на путь, у которого нет такого метода, - 405.

Источники параметров, обязательность и результаты. `@RequestMapping` на классе даёт общий префикс; обязательные параметры без значения дают 400:

```java run
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.HttpMethod;
import org.springframework.http.ResponseEntity;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    record Note(int id, String text) {}

    @RestController
    @RequestMapping("/api/v1")
    static class Demo {
        @GetMapping("/sum")
        int sum(@RequestParam int a, @RequestParam int b) { return a + b; }

        @GetMapping("/users/{id}")
        Map<String, Object> user(@PathVariable int id,
                                 @RequestParam(defaultValue = "false") boolean verbose,
                                 @RequestHeader(name = "X-Tenant", required = false) String tenant) {
            Map<String, Object> body = new LinkedHashMap<>();
            body.put("id", id);
            body.put("verbose", verbose);
            body.put("tenant", tenant);
            return body;
        }

        @PostMapping("/echo")
        ResponseEntity<Note> echo(@RequestBody Note note) {
            return ResponseEntity.created(URI.create("/api/v1/notes/" + note.id())).body(note);
        }

        @GetMapping("/legacy")
        ResponseEntity<Void> legacy() {
            return ResponseEntity.status(302).location(URI.create("/api/v1/sum?a=1&b=2")).build();
        }
    }

    // No component scan: the controller is registered explicitly, so a single file in the default package is enough.
    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import(Demo.class)
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            show(mvc, MockMvcRequestBuilders.get("/api/v1/sum?a=2&b=3"));
            show(mvc, MockMvcRequestBuilders.get("/api/v1/sum?a=2"));
            show(mvc, MockMvcRequestBuilders.get("/api/v1/sum?a=2&b=x"));
            show(mvc, MockMvcRequestBuilders.get("/api/v1/users/5?verbose=true").header("X-Tenant", "acme"));
            show(mvc, MockMvcRequestBuilders.get("/api/v1/users/5"));
            show(mvc, MockMvcRequestBuilders.post("/api/v1/echo").contentType("application/json").content("{\"id\":9,\"text\":\"hi\"}"));
            show(mvc, MockMvcRequestBuilders.post("/api/v1/echo").contentType("application/json").content("{broken"));
            show(mvc, MockMvcRequestBuilders.get("/api/v1/legacy"));
        }
    }

    static void show(MockMvc mvc, RequestBuilder request) throws Exception {
        MockHttpServletResponse response = mvc.perform(request).andReturn().getResponse();
        String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
        String location = response.getHeader("Location") == null ? "" : " Location=" + response.getHeader("Location");
        System.out.println((response.getStatus() + " " + type + location + " " + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
    }
}
```
```output
200 application/json 5
400 -
400 -
200 application/json {"id":5,"verbose":true,"tenant":"acme"}
200 application/json {"id":5,"verbose":false,"tenant":null}
201 application/json Location=/api/v1/notes/9 {"id":9,"text":"hi"}
400 -
302 - Location=/api/v1/sum?a=1&b=2
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать 404 от неподходящего типа переменной пути.** `/books/{id}` с `int id` и запрос `/books/abc` дают 400. Чтобы получить 404, ограничьте шаблон регулярным выражением: `{id:\d+}`.
* **Считать параметр запроса необязательным.** `@RequestParam int b` без значения в запросе - это 400; для необязательного используйте `required = false`, `defaultValue` или `Optional`.
* **Возвращать `null` вместо 404.** Метод, вернувший `null`, даёт пустой ответ 200. Если книги нет, возвращайте `ResponseEntity.notFound().build()`.
* **Забывать про 201 и `Location` при создании.** `ResponseEntity.created(URI.create("/books/" + id)).body(book)` выставляет и код, и адрес нового ресурса.
* **Хранить состояние запроса в полях контроллера.** Экземпляр один на все запросы и потоки; изменяемое поле без синхронизации - гонка данных.
* **Класс приложения в безымянном пакете.** Сканирование компонентов от класса в корневом пакете без имени охватило бы весь путь классов; держите `@SpringBootApplication` в именованном пакете.

<!-- section: summary -->
## Итоги

* `@SpringBootApplication` включает сканирование компонентов и автоконфигурацию; контроллеры помечаются `@RestController`.
* Метод HTTP и шаблон пути выбирают метод контроллера: нет пути - 404, нет метода - 405.
* Значения берутся аннотациями `@PathVariable`, `@RequestParam`, `@RequestHeader`, `@RequestBody`; обязательные параметры без значения и значения неподходящего типа дают 400.
* Простые ответы - значения и записи; `ResponseEntity` задаёт код (`created`, `notFound`, `noContent`) и заголовки.
* MockMvc позволяет проверять всё это без открытия порта.

Дальше: [тела запросов и валидация](lesson:java.spring.request-bodies-validation).
