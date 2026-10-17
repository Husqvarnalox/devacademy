<!-- section: simple -->
## Ошибки как часть контракта API

Клиент вашего API видит не стек вызовов, а **ответ**: код состояния и тело. Если сервис выбросил `AccountNotFoundException`, а клиент получил пустой ответ или страницу с трассировкой, он не поймёт, что случилось, а злоумышленник узнает лишнее. В Spring MVC исключение, вылетевшее из метода контроллера, можно перехватить и превратить в нужный ответ.

Есть три уровня:

* метод с `@ExceptionHandler` **внутри контроллера** обрабатывает исключения только этого контроллера;
* метод с `@ExceptionHandler` в классе `@RestControllerAdvice` (или `@ControllerAdvice`) - исключения **всех** контроллеров: это место для общего правила «исключение домена - код ответа»;
* исключение может само нести код: `ResponseStatusException` или собственный класс с `@ResponseStatus`.

Тело ошибки удобно строить по стандарту **RFC 9457** (Problem Details for HTTP APIs): JSON с полями `type`, `title`, `status`, `detail`, `instance` и медиатипом `application/problem+json`. В Spring его представляет класс `ProblemDetail`: обработчик просто возвращает такой объект, остальное делает фреймворк. Сравните с [ProblemDetails в ASP.NET Core](lesson:csharp.aspnetcore.error-handling-problem-details) и с [обработкой ошибок в Express](lesson:express.fundamentals.error-handling): идея та же, меняется только способ подключения.

<!-- section: precise -->
## Точные правила

**Выбор обработчика.** Исключение из метода контроллера сначала ищет обработчик в самом контроллере, затем в советах. Среди подходящих методов выигрывает тот, чей тип исключения ближе к брошенному: `@ExceptionHandler(AccountNotFoundException.class)` сильнее `@ExceptionHandler(Exception.class)`. Локальный обработчик контроллера побеждает совет даже тогда, когда обработчик в совете конкретнее. Два метода на один и тот же тип исключения в одном классе - ошибка конфигурации.

**ProblemDetail.** Создаётся через `ProblemDetail.forStatusAndDetail(HttpStatus, String)` или `forStatus(...)`. Поля: `type` (URI вида `https://example.com/problems/account-closed`; по умолчанию `about:blank`, и тогда поле не печатается), `title`, `status`, `detail`, `instance` (если не задан, Spring подставляет путь запроса). Свои поля добавляются через `setProperty("balance", 100)` и выводятся на верхнем уровне JSON рядом со стандартными. Метод, вернувший `ProblemDetail`, сам выставляет статус ответа и медиатип `application/problem+json`.

**Ошибки самого фреймворка.** Неверный путь (404), метод (405), тип содержимого (415), параметр (400), нечитаемый JSON (400) бросает Spring MVC. По умолчанию свойство `spring.mvc.problemdetails.enabled` выключено, и в MockMvc такие ответы приходят **с пустым телом**. Два способа получить `ProblemDetail`: включить свойство или объявить совет, наследующий `ResponseEntityExceptionHandler` (он обрабатывает все исключения Spring MVC и ответы вида `ErrorResponse`). Свои `@ExceptionHandler` для доменных исключений вы пишете в том же классе.

**ResponseStatusException и @ResponseStatus.** `throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "причина")` задаёт код прямо в коде метода; через `ResponseEntityExceptionHandler` он превращается в ProblemDetail. `@ResponseStatus(HttpStatus.CONFLICT)` на классе исключения даёт код без обработчика, но только как **запасной вариант**: если совет перехватил исключение, в том числе общим `@ExceptionHandler(RuntimeException.class)`, аннотация не применяется и тело формируете вы.

**Выбор кодов.** Не нашли ресурс - 404. Запрос корректен, но противоречит текущему состоянию (закрытый счёт, дубликат имени) - 409. Запрос понятен и состояние допускает операцию, но данные нарушают бизнес-правило (нехватка средств) - 422. Неожиданная ошибка - 500.

**Не раскрывайте внутренности.** `e.getMessage()` неожиданного исключения может содержать адреса, SQL и секреты, а трассировка раскрывает устройство кода. Для 500 отдавайте фиксированный `detail` («Unexpected error»), а подробности записывайте в журнал сервера. В MockMvc исключение без обработчика вылетает из `perform(...)` вызывающему коду: настоящий сервер ответил бы 500.

<!-- section: examples -->
## Примеры

Что отдаёт Spring Boot без своего совета, и что меняет свойство `spring.mvc.problemdetails.enabled`. Один и тот же набор запросов выполняется в двух контекстах:

```java run
import java.nio.charset.StandardCharsets;
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
import org.springframework.test.web.servlet.RequestBuilder;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

public class Main {
    record Note(String text) {}

    @RestController
    static class Demo {
        @GetMapping("/notes/{id}")
        String note(@PathVariable int id) { return "note " + id; }

        @PostMapping("/notes")
        Note create(@RequestBody Note note) { return note; }

        @GetMapping("/boom")
        String boom() { throw new IllegalStateException("secret: db password"); }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import(Demo.class)
    static class Config {}

    public static void main(String[] args) throws Exception {
        for (String flag : new String[] {"false", "true"}) {
            System.out.println("spring.mvc.problemdetails.enabled=" + flag);
            try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                    .web(WebApplicationType.SERVLET)
                    .bannerMode(Banner.Mode.OFF)
                    .logStartupInfo(false)
                    .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                    .run("--logging.level.root=OFF", "--spring.mvc.problemdetails.enabled=" + flag)) {
                MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
                show(mvc, "GET /nope", MockMvcRequestBuilders.get("/nope"));
                show(mvc, "DELETE /notes/1", MockMvcRequestBuilders.delete("/notes/1"));
                show(mvc, "GET /notes/abc", MockMvcRequestBuilders.get("/notes/abc"));
                show(mvc, "POST text/plain", MockMvcRequestBuilders.post("/notes").contentType("text/plain").content("x"));
                show(mvc, "POST broken json", MockMvcRequestBuilders.post("/notes").contentType("application/json").content("{broken"));
                show(mvc, "GET /boom", MockMvcRequestBuilders.get("/boom"));
            }
        }
    }

    static void show(MockMvc mvc, String label, RequestBuilder request) throws Exception {
        String result;
        try {
            MockHttpServletResponse response = mvc.perform(request).andReturn().getResponse();
            String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
            result = (response.getStatus() + " " + type + " " + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing();
        } catch (Exception e) {
            // MockMvc has no server around it, so an exception nobody handled is rethrown to the caller.
            result = "exception escaped: " + e.getCause();
        }
        System.out.println(String.format("  %-18s %s", label, result));
    }
}
```
```output
spring.mvc.problemdetails.enabled=false
  GET /nope          404 -
  DELETE /notes/1    405 -
  GET /notes/abc     400 -
  POST text/plain    415 -
  POST broken json   400 -
  GET /boom          exception escaped: java.lang.IllegalStateException: secret: db password
spring.mvc.problemdetails.enabled=true
  GET /nope          404 application/problem+json {"detail":"No static resource nope.","instance":"/nope","status":404,"title":"Not Found"}
  DELETE /notes/1    405 application/problem+json {"detail":"Method 'DELETE' is not supported.","instance":"/notes/1","status":405,"title":"Method Not Allowed"}
  GET /notes/abc     400 application/problem+json {"detail":"Failed to convert 'id' with value: 'abc'","instance":"/notes/abc","status":400,"title":"Bad Request"}
  POST text/plain    415 application/problem+json {"detail":"Content-Type 'text/plain' is not supported.","instance":"/notes","status":415,"title":"Unsupported Media Type"}
  POST broken json   400 application/problem+json {"detail":"Failed to read request","instance":"/notes","status":400,"title":"Bad Request"}
  GET /boom          exception escaped: java.lang.IllegalStateException: secret: db password
```

Обратите внимание на три вещи. Во-первых, по умолчанию тела пустые (`404 -`: тип содержимого отсутствует). Во-вторых, детали вроде `No static resource nope.` берутся из исключений Spring MVC. В-третьих, исключение контроллера (`/boom`) не превращается в ответ ни в одном из режимов: его сообщение, в котором спрятан «пароль», вылетело в вызывающий код. Именно поэтому нужен свой обработчик общего `Exception`, возвращающий фиксированный текст.

Совет, обработчики разной конкретности, локальный обработчик, `ResponseStatusException` и `@ResponseStatus`. `DomainException` - базовый класс, `NotFound` и `Stale` - его наследники:

```java run
import java.net.URI;
import java.nio.charset.StandardCharsets;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

public class Main {
    static class DomainException extends RuntimeException {
        DomainException(String message) { super(message); }
    }

    static class NotFound extends DomainException {
        NotFound(String what) { super(what); }
    }

    static class Stale extends DomainException {
        Stale() { super("stale write"); }
    }

    @ResponseStatus(HttpStatus.CONFLICT)
    static class Duplicate extends RuntimeException {}

    @RestController
    static class Items {
        @GetMapping("/items/{id}")
        String item(@PathVariable int id) {
            if (id == 404) throw new NotFound("item " + id);
            if (id == 409) throw new Duplicate();
            if (id == 410) throw new Stale();
            if (id == 400) throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "id 400 is reserved");
            return "item " + id;
        }
    }

    @RestController
    static class Local {
        @GetMapping("/local")
        String local() { throw new NotFound("local"); }

        @ExceptionHandler(NotFound.class)
        ProblemDetail own(NotFound e) {
            return ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, "handled in the controller");
        }
    }

    @RestControllerAdvice
    static class Advice extends ResponseEntityExceptionHandler {
        @ExceptionHandler(NotFound.class)
        ProblemDetail notFound(NotFound e) {
            ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, "Not found: " + e.getMessage());
            problem.setType(URI.create("https://example.com/problems/not-found"));
            problem.setTitle("Resource not found");
            problem.setProperty("code", "E404");
            return problem;
        }

        @ExceptionHandler(DomainException.class)
        ProblemDetail anyDomain(DomainException e) {
            return ProblemDetail.forStatusAndDetail(HttpStatus.UNPROCESSABLE_CONTENT, "Handled by the DomainException method");
        }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import({Items.class, Local.class, Advice.class})
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            show(mvc, "GET /items/7", MockMvcRequestBuilders.get("/items/7"));
            show(mvc, "GET /items/404", MockMvcRequestBuilders.get("/items/404"));
            show(mvc, "GET /items/410", MockMvcRequestBuilders.get("/items/410"));
            show(mvc, "GET /items/409", MockMvcRequestBuilders.get("/items/409"));
            show(mvc, "GET /items/400", MockMvcRequestBuilders.get("/items/400"));
            show(mvc, "GET /local", MockMvcRequestBuilders.get("/local"));
            show(mvc, "GET /items/x", MockMvcRequestBuilders.get("/items/x"));
        }
    }

    static void show(MockMvc mvc, String label, RequestBuilder request) throws Exception {
        MockHttpServletResponse response = mvc.perform(request).andReturn().getResponse();
        String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
        System.out.println(String.format("%-16s %s", label,
                (response.getStatus() + " " + type + " " + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing()));
    }
}
```
```output
GET /items/7     200 text/plain item 7
GET /items/404   404 application/problem+json {"detail":"Not found: item 404","instance":"/items/404","status":404,"title":"Resource not found","type":"https://example.com/problems/not-found","code":"E404"}
GET /items/410   422 application/problem+json {"detail":"Handled by the DomainException method","instance":"/items/410","status":422,"title":"Unprocessable Content"}
GET /items/409   409 -
GET /items/400   400 application/problem+json {"detail":"id 400 is reserved","instance":"/items/400","status":400,"title":"Bad Request"}
GET /local       404 application/problem+json {"detail":"handled in the controller","instance":"/local","status":404,"title":"Not Found"}
GET /items/x     400 application/problem+json {"detail":"Failed to convert 'id' with value: 'x'","instance":"/items/x","status":400,"title":"Bad Request"}
```

Разберите вывод. `/items/404` обработан конкретным методом для `NotFound` (с `type` и своим полем `code`), а `/items/410` - `Stale` без своего метода - попал в общий метод для `DomainException`. `/items/409`: исключение `Duplicate` не наследует `DomainException`, совет его не перехватывает, и работает запасная `@ResponseStatus`: 409 без тела (`-`). `/items/400` - `ResponseStatusException`, обработанное базовым классом; `/items/x` - ошибка преобразования типа, тоже в виде ProblemDetail. Метод `Local` перехватил `NotFound` сам, хотя в совете есть более подробный ответ.

<!-- section: mistakes -->
## Типичные ошибки

* **Отдавать `e.getMessage()` для неожиданных ошибок.** Сообщение исключения - для журнала, а не для клиента. Для 500 используйте фиксированный `detail`.
* **Общий `@ExceptionHandler(RuntimeException.class)` «съедает» `@ResponseStatus`.** Аннотация на классе исключения срабатывает, только если никакой обработчик его не перехватил. Если нужен ответ для своего типа, напишите отдельный метод.
* **Ждать тела ошибки от фреймворка по умолчанию.** Без свойства `spring.mvc.problemdetails.enabled` или совета на `ResponseEntityExceptionHandler` ответы 404, 405, 415 и 400 приходят пустыми. Сделайте формат ошибок единым: либо включите свойство, либо наследуйте базовый класс.
* **Путать 409 и 422.** 409 - конфликт с текущим состоянием ресурса, 422 - данные не проходят бизнес-правило; 404 при этом не «запасной» код для любых проблем.
* **Рассчитывать на порядок методов в файле.** Выбор обработчика определяется близостью типов исключений и местом (контроллер раньше совета), а не порядком объявления.
* **Класть в `type` не URI.** Поле `type` - идентификатор вида ошибки в виде URI; клиентский код сравнивает его как значение, поэтому оно должно быть стабильным.

<!-- section: summary -->
## Итоги

* Исключения превращаются в ответы методами `@ExceptionHandler`: в контроллере - для него одного, в `@RestControllerAdvice` - для всех.
* Побеждает обработчик контроллера, затем самый конкретный тип исключения в совете; `@ResponseStatus` - запасной вариант.
* `ProblemDetail` реализует RFC 9457: `type`, `title`, `status`, `detail`, `instance`, свои поля через `setProperty`, медиатип `application/problem+json`.
* Чтобы ошибки самого фреймворка тоже были ProblemDetail, включите `spring.mvc.problemdetails.enabled` или наследуйте `ResponseEntityExceptionHandler`.
* Отдавайте 404 для отсутствующего ресурса, 409 для конфликта состояния, 422 для нарушенного бизнес-правила, а в 500 не показывайте сообщений и трассировок.

Дальше: [тестирование через MockMvc](lesson:java.spring.testing-with-mockmvc).
