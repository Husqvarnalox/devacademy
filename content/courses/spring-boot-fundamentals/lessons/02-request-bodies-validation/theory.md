<!-- section: simple -->
## Тело запроса и проверка данных

Клиент отправляет JSON, а контроллеру нужен объект. Аннотация `@RequestBody` говорит Spring: «возьми тело запроса и преврати его в значение этого параметра». Превращением занимается **Jackson 3** (пакеты `tools.jackson.*`; аннотации вроде `@JsonProperty` остались в `com.fasterxml.jackson.annotation`). Для данных запроса удобнее всего `record`: имена компонентов записи становятся именами JSON-полей.

Но «JSON разобрался» ещё не значит «данные годятся». Правила описывают **ограничениями Bean Validation** (пакет `jakarta.validation.constraints`) прямо на компонентах записи, а проверку включает аннотация `@Valid` на параметре:

```java
record Signup(
        @NotBlank(message = "name is required") String name,
        @Min(value = 18, message = "must be 18+") Integer age) {}

@PostMapping("/signup")
Signup signup(@Valid @RequestBody Signup request) { ... }
```

Если нарушено хотя бы одно ограничение, метод контроллера **не вызывается**: Spring бросает `MethodArgumentNotValidException`, и клиент получает 400. Если тело вообще не разбирается (оборванный JSON, строка вместо числа), это тоже 400, но другое исключение: `HttpMessageNotReadableException`. А если клиент прислал тело не того типа (`text/plain` вместо JSON), ответ 415.

В коде валидации нет: ограничения - декларативные метки на данных, а проверяет их библиотека Hibernate Validator, которую подключает `spring-boot-starter-validation`.

<!-- section: precise -->
## Точные правила

**Три разные ошибки.** Запрос с телом может провалиться на трёх этапах, и каждому соответствует своё исключение и свой код:

* заголовок `Content-Type` не поддерживается - `HttpMediaTypeNotSupportedException`, **415**;
* тело нельзя прочитать или преобразовать (синтаксическая ошибка, значение неподходящего типа, пустое тело при обязательном `@RequestBody`, примитив без значения) - `HttpMessageNotReadableException`, **400**;
* тело прочитано, но нарушает ограничения - `MethodArgumentNotValidException`, **400**; аргумент должен быть помечен `@Valid` (или `@Validated`), иначе проверки не будет вовсе.

Все три стандартная настройка Spring Boot превращает в ответ **без тела**: видно только код. Читаемый JSON с описанием ошибок вы делаете сами (пример ниже) либо включаете стандартный формат ProblemDetail свойством `spring.mvc.problemdetails.enabled=true`: тогда ответ получает тело с полями `status`, `title`, `detail` и `instance`, но без списка нарушенных полей.

**Ограничения.** Самые частые:

* `@NotNull` - значение не `null`; `@NotBlank` - строка не `null`, не пустая и не из одних пробелов; `@NotEmpty` - строка или коллекция не пустая;
* `@Size(min, max)` - длина строки или размер коллекции; `@Min`, `@Max`, `@Positive` - числа; `@Email`, `@Pattern(regexp = ...)` - формат строки.

Важное свойство: почти все ограничения **считают `null` допустимым** и проверяют значение, только если оно есть. Поэтому `@Size(min = 3)` пропустит отсутствующее поле; обязательность задаёт отдельный `@NotNull`/`@NotBlank`. Для чисел, которые могут отсутствовать, берите `Integer`, а не `int`: примитив без значения Jackson отвергает ещё при разборе (400), и до ваших сообщений дело не дойдёт.

**Вложенные объекты и коллекции.** Ограничения внутри вложенной записи проверяются, только если поле помечено `@Valid`. Для элементов коллекции аннотацию ставят на параметр типа: `List<@Valid Line>`, `List<@NotBlank String>`. Имя нарушенного поля содержит путь: `address.city`, `lines[1].qty`, `tags[1]`.

**Тексты сообщений.** Если не указать `message`, текст берётся из ресурсов библиотеки и зависит от языка JVM. Для стабильного API (и для тестов) задавайте `message = "..."` явно или ключи собственного набора сообщений. В этом курсе все сообщения явные.

**Ограничения на простых параметрах.** Ограничения можно ставить и на `@PathVariable`/`@RequestParam`/`@RequestHeader`: `@RequestParam @Size(max = 3) String q`. В Spring 6.1+ такая проверка встроена (сам `@Validated` на классе не нужен), нарушение даёт `HandlerMethodValidationException` и 400.

**Лишние и недостающие поля.** Spring Boot настраивает Jackson так, что **неизвестные свойства JSON игнорируются**. Строгий режим включает свойство `spring.jackson.deserialization.fail-on-unknown-properties=true`: тогда лишнее поле даёт 400. Отсутствующее поле для ссылочного типа превращается в `null` (и его ловит `@NotNull`).

**Порядок нарушений не определён.** Если у одного поля нарушены два ограничения, нельзя полагаться на то, какое сообщение придёт первым. Обработчик, который собирает ошибки, должен сам выбирать правило (например, сортировать), чтобы ответ был одинаковым при каждом запуске.

<!-- section: examples -->
## Примеры

Стандартное поведение без собственной обработки ошибок. Для каждого запроса печатаются код ответа и класс исключения, которое нашла инфраструктура Spring MVC:

```java run
import java.nio.charset.StandardCharsets;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    record Person(
            @NotBlank(message = "name is required") String name,
            @NotNull(message = "age is required") @Min(value = 18, message = "must be 18+") Integer age) {}

    @RestController
    static class Demo {
        @PostMapping("/checked")
        Person checked(@Valid @RequestBody Person person) { return person; }

        @PostMapping("/unchecked")
        Person unchecked(@RequestBody Person person) { return person; }

        @GetMapping("/items/{id}")
        String item(@PathVariable @Min(value = 1, message = "id must be positive") int id,
                    @RequestParam @Size(max = 3, message = "q is too long") String q) {
            return "item " + id + " " + q;
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
            post(mvc, "/checked", "application/json", "{\"name\":\"Ann\",\"age\":30}");
            post(mvc, "/checked", "application/json", "{\"name\":\" \",\"age\":12}");
            post(mvc, "/unchecked", "application/json", "{\"name\":\" \",\"age\":12}");
            post(mvc, "/checked", "application/json", "{\"name\":\"Ann\",\"age\":30,\"vip\":true}");
            post(mvc, "/checked", "application/json", "{\"name\":\"Ann\",\"age\":\"old\"}");
            post(mvc, "/checked", "application/json", "{\"name\":\"Ann\"");
            post(mvc, "/checked", "application/json", "");
            post(mvc, "/checked", "text/plain", "Ann");
            get(mvc, "/items/5?q=abc");
            get(mvc, "/items/0?q=abc");
            get(mvc, "/items/5?q=abcd");
        }
    }

    static void post(MockMvc mvc, String path, String type, String body) throws Exception {
        print(mvc.perform(MockMvcRequestBuilders.post(path).contentType(type).content(body)).andReturn(), "POST " + path + " " + body);
    }

    static void get(MockMvc mvc, String uri) throws Exception {
        print(mvc.perform(MockMvcRequestBuilders.get(uri)).andReturn(), "GET " + uri);
    }

    static void print(MvcResult result, String label) throws Exception {
        MockHttpServletResponse response = result.getResponse();
        Exception error = result.getResolvedException();
        System.out.println((label + " -> " + response.getStatus() + " "
                + (error == null ? "-" : error.getClass().getSimpleName()) + " "
                + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
    }
}
```
```output
POST /checked {"name":"Ann","age":30} -> 200 - {"name":"Ann","age":30}
POST /checked {"name":" ","age":12} -> 400 MethodArgumentNotValidException
POST /unchecked {"name":" ","age":12} -> 200 - {"name":" ","age":12}
POST /checked {"name":"Ann","age":30,"vip":true} -> 200 - {"name":"Ann","age":30}
POST /checked {"name":"Ann","age":"old"} -> 400 HttpMessageNotReadableException
POST /checked {"name":"Ann" -> 400 HttpMessageNotReadableException
POST /checked  -> 400 HttpMessageNotReadableException
POST /checked Ann -> 415 HttpMediaTypeNotSupportedException
GET /items/5?q=abc -> 200 - item 5 abc
GET /items/0?q=abc -> 400 HandlerMethodValidationException
GET /items/5?q=abcd -> 400 HandlerMethodValidationException
```

Обратите внимание на три вещи. Метод `/unchecked` без `@Valid` принял пустое имя и возраст 12: без аннотации проверки нет. Лишнее поле `vip` молча отброшено. А три разных вида поломки тела и параметров дают четыре разных исключения, но тела ответа у всех нет.

Теперь сделаем ответ полезным: `@RestControllerAdvice` с `@ExceptionHandler` ловит `MethodArgumentNotValidException` во всех контроллерах и возвращает JSON «поле - сообщение». Заодно видны вложенные записи, элементы коллекций и строгий режим для неизвестных полей:

```java run
import java.nio.charset.StandardCharsets;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import org.springframework.http.ResponseEntity;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    record Line(@NotBlank(message = "sku is required") String sku, @Positive(message = "qty must be positive") int qty) {}

    record Order(
            @NotBlank(message = "customer is required") String customer,
            @NotEmpty(message = "at least one line is required") List<@Valid Line> lines,
            @Size(max = 2, message = "at most 2 tags") List<@NotBlank(message = "tag must not be blank") String> tags) {}

    record Created(String customer, int lines) {}

    @RestController
    static class Demo {
        @PostMapping("/orders")
        ResponseEntity<Created> create(@Valid @RequestBody Order order) {
            return ResponseEntity.status(201).body(new Created(order.customer(), order.lines().size()));
        }
    }

    // One handler turns every failed @Valid body into the same small JSON: field -> message.
    @RestControllerAdvice
    static class Errors {
        @ExceptionHandler(MethodArgumentNotValidException.class)
        ResponseEntity<Map<String, Object>> invalid(MethodArgumentNotValidException e) {
            Map<String, String> errors = new TreeMap<>();   // sorted: the order of violations itself is not specified
            e.getBindingResult().getFieldErrors().forEach(error -> errors.merge(error.getField(), error.getDefaultMessage(),
                    (a, b) -> a.compareTo(b) <= 0 ? a : b));
            return ResponseEntity.badRequest().body(Map.of("errors", errors));
        }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import({Demo.class, Errors.class})
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                // Boot ignores unknown JSON properties by default; this switch makes them an error (400).
                .run("--logging.level.root=OFF", "--spring.jackson.deserialization.fail-on-unknown-properties=true")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            post(mvc, "{\"customer\":\"Ann\",\"lines\":[{\"sku\":\"A1\",\"qty\":2}],\"tags\":[\"gift\"]}");
            post(mvc, "{\"customer\":\"\",\"lines\":[]}");
            post(mvc, "{\"customer\":\"Ann\",\"lines\":[{\"sku\":\"A1\",\"qty\":2},{\"sku\":\"\",\"qty\":0}],\"tags\":[\"a\",\" \",\"c\"]}");
            post(mvc, "{\"customer\":\"Ann\",\"lines\":[{\"sku\":\"A1\",\"qty\":1}],\"coupon\":\"X\"}");
        }
    }

    static void post(MockMvc mvc, String body) throws Exception {
        MockHttpServletResponse response = mvc.perform(MockMvcRequestBuilders.post("/orders")
                .contentType("application/json").content(body)).andReturn().getResponse();
        String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
        System.out.println((response.getStatus() + " " + type + " " + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
    }
}
```
```output
201 application/json {"customer":"Ann","lines":1}
400 application/json {"errors":{"customer":"customer is required","lines":"at least one line is required"}}
400 application/json {"errors":{"lines[1].qty":"qty must be positive","lines[1].sku":"sku is required","tags":"at most 2 tags","tags[1]":"tag must not be blank"}}
400 -
```

Ключи `lines[1].sku` и `tags[1]` показывают, откуда пришло нарушение. В последнем запросе лишнее поле `coupon` из-за строгого режима дало 400 без нашего JSON: это `HttpMessageNotReadableException`, а наш обработчик отвечает только за нарушения ограничений. Аналогичную работу в ASP.NET Core делает атрибутная валидация и `ModelState` (см. [привязку модели и валидацию](lesson:csharp.aspnetcore.model-binding-validation)); в Node.js за это отвечают схемы вроде Zod (см. [валидацию и безопасность](lesson:express.fundamentals.validation-security)). Отличие Spring: проверка включается аннотацией `@Valid` на параметре, а при её отсутствии данные проходят без проверки.

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть `@Valid`.** Ограничения на записи без `@Valid` на параметре не проверяются никогда, а ошибки не видно: код просто работает с неверными данными.
* **Ждать, что `@Size` или `@Email` требуют значение.** Они пропускают `null`. Обязательность - это `@NotNull`, `@NotBlank` или `@NotEmpty`.
* **Поставить `@Min` на `int`, а не на `Integer`, и удивляться 400.** Отсутствующее число-примитив падает при разборе JSON до проверки ограничений.
* **Забыть `@Valid` на вложенной записи или `<@Valid T>` на элементах списка.** Тогда ограничения внутри них молчат.
* **Опираться на текст сообщения по умолчанию.** Он зависит от языка JVM; задавайте `message` сами.
* **Полагаться на порядок нарушений.** Для одного поля и для набора полей он не гарантирован: сортируйте или выбирайте сообщение по явному правилу.
* **Показывать клиенту внутренности исключения.** Возвращайте поле и короткое сообщение, а не `e.getMessage()` или трассировку стека.

<!-- section: summary -->
## Итоги

* `@RequestBody` превращает JSON в запись; `@Valid` включает проверку ограничений `jakarta.validation` на её компонентах.
* Нечитаемое тело - 400 (`HttpMessageNotReadableException`), неверный тип содержимого - 415, нарушенное ограничение - 400 (`MethodArgumentNotValidException`); тело ответа по умолчанию пустое.
* Ограничения пропускают `null`, обязательность задают `@NotNull`/`@NotBlank`; вложенное и элементы списков проверяются только с `@Valid`.
* Сообщения задавайте явно, порядок нарушений не определён; `@RestControllerAdvice` превращает ошибки в понятный JSON.

Дальше: [бины и внедрение зависимостей](lesson:java.spring.beans-dependency-injection).
