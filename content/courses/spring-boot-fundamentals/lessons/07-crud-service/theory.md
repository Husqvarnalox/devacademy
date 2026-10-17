<!-- section: simple -->
## Сервис из трёх слоёв

Небольшой REST-сервис удобно делить на три слоя, и каждый отвечает за своё:

* **контроллер** знает про HTTP: пути, коды ответов, заголовки, превращение JSON в объект и обратно;
* **сервис** знает правила предметной области: «нет такого товара», «нельзя заменить несуществующий»; про HTTP он не знает;
* **репозиторий** знает, где лежат данные. Это интерфейс (`findById`, `save`, `deleteById`), а реализация может быть любой: в уроке это `ConcurrentHashMap` в памяти.

Контроллер вызывает сервис, сервис вызывает репозиторий; Spring соединяет их через внедрение зависимостей (аннотации `@RestController`, `@Service`, `@Repository`, конструкторы без `@Autowired`). Так код проще проверять и менять: другое хранилище не затрагивает контроллер.

Для ресурса `products` набор операций стандартный (REST):

| Запрос | Что делает | Успех | Ошибки |
|---|---|---|---|
| `GET /products` | страница списка | 200 | 400 (плохие `page`, `size`, `sort`) |
| `GET /products/{id}` | один товар | 200 | 404 |
| `POST /products` | создать | 201 + `Location` | 400 |
| `PUT /products/{id}` | заменить целиком | 200 | 400, 404 |
| `DELETE /products/{id}` | удалить | 204 | 404 |

Тело запроса - **DTO** (data transfer object): запись без `id`, которая описывает только то, что вправе прислать клиент. Внутренняя сущность может иметь поля, которые наружу не выходят (себестоимость, служебные флаги), - поэтому наружу отдают отдельный ответный DTO. Сравните с [тестируемым CRUD-сервисом в ASP.NET Core](lesson:csharp.aspnetcore.testing-crud-service) и с [REST API на Express](lesson:express.fundamentals.rest-api): слои и статусы те же.

<!-- section: precise -->
## Точные правила

**Создание.** `POST /products` создаёт ресурс; сервер назначает `id`. Ответ: `201 Created`, заголовок `Location` с адресом нового ресурса и (по желанию) само представление: `ResponseEntity.created(URI.create("/products/" + id)).body(product)`.

**Замена.** `PUT /products/{id}` заменяет ресурс **целиком**: все поля берутся из тела, и поля, которые клиент не прислал, не остаются прежними (в уроке проверка `@Valid` требует их присутствия). Если ресурса нет, ответ 404 (допустима и схема «PUT создаёт», но тогда ответ 201; в уроке выбрано 404). Частичное обновление - `PATCH`: Spring MVC не мешает, но слияние полей вы пишете сами; в уроке `PATCH` не используется.

**Удаление.** `DELETE` возвращает `204 No Content` без тела; повторное удаление несуществующего ресурса даёт 404.

**Идемпотентность.** Метод идемпотентен, если повторение запроса приводит к тому же состоянию сервера, что и одно выполнение (RFC 9110). `GET`, `PUT` и `DELETE` идемпотентны, `POST` нет: два одинаковых `POST` создадут два товара. Идемпотентность не требует одинаковых кодов: первый `DELETE` вернёт 204, второй - 404, а состояние (ресурса нет) одно и то же.

**Проверка входных данных.** Правила описывают аннотациями на полях DTO (`@NotBlank`, `@Positive`) и включают параметром `@Valid` перед `@RequestBody`; нарушение даёт 400 (`MethodArgumentNotValidException`). Одни и те же правила работают для `POST` и `PUT`, потому что тело у них общее. Проверка идёт **до** вызова метода: `PUT /products/99` с неверным телом получит 400, а не 404.

**Ошибки.** Сервис бросает доменные исключения (`ProductNotFoundException`), а один `@RestControllerAdvice` (урок про `ProblemDetail`) превращает их в 404 с телом ProblemDetail. Контроллер при этом остаётся без `if (x == null)`.

**Страницы и сортировка.** `GET /products?page=1&size=2&sort=price,desc`: номер страницы с нуля, размер страницы, поле и направление. Класс `Pageable` из Spring Data в этом курсе недоступен (Spring Data не входит в набор библиотек), поэтому параметры разбираются вручную. Правила стоит проверять: `size` ограничивают сверху (иначе клиент попросит миллиард записей), неизвестное поле сортировки даёт 400, а не 500. Чтобы порядок был предсказуем, при равных значениях добавляют вторичный ключ - `id`. Ответ содержит не только элементы, но и `page`, `size`, `total`.

**Потокобезопасность.** Бины Spring - одиночки, один экземпляр репозитория обслуживает все потоки. `HashMap` и счётчик `long++` в такой обстановке ломаются: теряются записи и повторяются id. Используйте `ConcurrentHashMap` и `AtomicLong` (сам объект `Product` - неизменяемая запись, поэтому безопасен).

**Условные запросы (для задачи повышенной сложности).** Заголовок `ETag` - метка версии представления. Клиент присылает её в `If-None-Match` при `GET`: если метка совпала, сервер отвечает `304 Not Modified` без тела. В `PUT` присылают `If-Match`: если метка не совпала, значит ресурс успели изменить, и сервер отвечает `412 Precondition Failed`. В Spring MVC обе проверки делает метод `WebRequest.checkNotModified(etag)`.

<!-- section: examples -->
## Примеры

Три слоя, DTO и сущность, коды ответов. Репозиторий - интерфейс с реализацией на `ConcurrentHashMap`; сервис бросает `ResponseStatusException(404)`; контроллер собирает ответ. Заметьте, что сущность хранит `costPrice`, а ответ его не содержит:

```java run
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.stereotype.Repository;
import org.springframework.stereotype.Service;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;
import org.springframework.web.server.ResponseStatusException;

public class Main {
    // Entity: the internal model, it has a field the API must not show.
    record ProductEntity(long id, String name, int price, int costPrice) {}

    // DTOs: what travels over HTTP. The request has no id and no cost; the response has no cost.
    record ProductRequest(@NotBlank String name, @Positive int price) {}

    record ProductResponse(long id, String name, int price) {
        static ProductResponse of(ProductEntity e) { return new ProductResponse(e.id(), e.name(), e.price()); }
    }

    // Repository: an interface plus an in-memory implementation that is safe for concurrent requests.
    interface ProductRepository {
        Optional<ProductEntity> findById(long id);
        ProductEntity save(ProductEntity entity);
        boolean deleteById(long id);
    }

    @Repository
    static class InMemoryProductRepository implements ProductRepository {
        private final ConcurrentHashMap<Long, ProductEntity> data = new ConcurrentHashMap<>();
        private final AtomicLong ids = new AtomicLong();

        public Optional<ProductEntity> findById(long id) { return Optional.ofNullable(data.get(id)); }

        public ProductEntity save(ProductEntity e) {
            long id = e.id() == 0 ? ids.incrementAndGet() : e.id();
            ProductEntity stored = new ProductEntity(id, e.name(), e.price(), e.costPrice());
            data.put(id, stored);
            return stored;
        }

        public boolean deleteById(long id) { return data.remove(id) != null; }
    }

    // Service: rules and the "not found" decision; it knows nothing about HTTP except the exception type.
    @Service
    static class ProductService {
        private final ProductRepository repository;

        ProductService(ProductRepository repository) { this.repository = repository; }

        ProductEntity get(long id) {
            return repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
        }

        ProductEntity create(ProductRequest r) { return repository.save(new ProductEntity(0, r.name(), r.price(), r.price() / 2)); }

        ProductEntity replace(long id, ProductRequest r) {
            ProductEntity old = get(id);
            return repository.save(new ProductEntity(id, r.name(), r.price(), old.costPrice()));
        }

        void delete(long id) {
            if (!repository.deleteById(id)) throw new ResponseStatusException(HttpStatus.NOT_FOUND);
        }
    }

    // Controller: HTTP only: paths, status codes, headers, DTO conversion.
    @RestController
    @RequestMapping("/products")
    static class ProductController {
        private final ProductService service;

        ProductController(ProductService service) { this.service = service; }

        @PostMapping
        ResponseEntity<ProductResponse> create(@Valid @RequestBody ProductRequest request) {
            ProductResponse body = ProductResponse.of(service.create(request));
            return ResponseEntity.created(URI.create("/products/" + body.id())).body(body);
        }

        @GetMapping("/{id}")
        ProductResponse get(@PathVariable long id) { return ProductResponse.of(service.get(id)); }

        @PutMapping("/{id}")
        ProductResponse replace(@PathVariable long id, @Valid @RequestBody ProductRequest request) {
            return ProductResponse.of(service.replace(id, request));
        }

        @DeleteMapping("/{id}")
        ResponseEntity<Void> delete(@PathVariable long id) {
            service.delete(id);
            return ResponseEntity.noContent().build();
        }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import({InMemoryProductRepository.class, ProductService.class, ProductController.class})
    static class Config {}

    public static void main(String[] args) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            send(mvc, "POST", "/products", "{\"name\":\"Tea\",\"price\":250}");
            send(mvc, "POST", "/products", "{\"name\":\"\",\"price\":250}");
            send(mvc, "GET", "/products/1", null);
            send(mvc, "PUT", "/products/1", "{\"name\":\"Green tea\",\"price\":300}");
            send(mvc, "PUT", "/products/1", "{\"name\":\"Green tea\",\"price\":300}");
            send(mvc, "PUT", "/products/7", "{\"name\":\"Ghost\",\"price\":1}");
            send(mvc, "DELETE", "/products/1", null);
            send(mvc, "DELETE", "/products/1", null);
            send(mvc, "GET", "/products/1", null);
        }
    }

    static void send(MockMvc mvc, String method, String path, String body) throws Exception {
        MockHttpServletRequestBuilder request = MockMvcRequestBuilders.request(HttpMethod.valueOf(method), path);
        if (body != null) request.contentType("application/json").content(body);
        MockHttpServletResponse response = mvc.perform(request).andReturn().getResponse();
        String location = response.getHeader("Location") == null ? "" : " Location=" + response.getHeader("Location");
        System.out.println(String.format("%-6s %-12s -> %d%s %s", method, path, response.getStatus(), location,
                response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
    }
}
```
```output
POST   /products    -> 201 Location=/products/1 {"id":1,"name":"Tea","price":250}
POST   /products    -> 400
GET    /products/1  -> 200 {"id":1,"name":"Tea","price":250}
PUT    /products/1  -> 200 {"id":1,"name":"Green tea","price":300}
PUT    /products/1  -> 200 {"id":1,"name":"Green tea","price":300}
PUT    /products/7  -> 404
DELETE /products/1  -> 204
DELETE /products/1  -> 404
GET    /products/1  -> 404
```

Идемпотентность видна в выводе: два одинаковых `PUT` дают один и тот же ответ, а повторный `DELETE` - 404 вместо 204, но состояние одно и то же. Пустое имя дало 400 (тело без `ProblemDetail`, так как в этом примере нет совета для ошибок). Об этом подробно в предыдущем уроке.

Страницы и сортировка вручную и потокобезопасное хранилище. Сначала 8 потоков добавляют по 500 записей: `AtomicLong` не повторяет id, `ConcurrentHashMap` ничего не теряет. Затем тот же набор данных отдаётся постранично:

```java run
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;
import org.springframework.web.server.ResponseStatusException;

public class Main {
    record Item(long id, String name, int price) {}

    record Page<T>(List<T> items, int page, int size, long total) {}

    static final ConcurrentHashMap<Long, Item> DATA = new ConcurrentHashMap<>();
    static final AtomicLong IDS = new AtomicLong();

    static Item add(String name, int price) {
        long id = IDS.incrementAndGet();
        Item item = new Item(id, name, price);
        DATA.put(id, item);
        return item;
    }

    @RestController
    static class Items {
        // Spring Data's Pageable is not available here, so ?page=&size=&sort=field,dir is parsed by hand.
        @GetMapping("/items")
        Page<Item> list(@RequestParam(defaultValue = "0") int page,
                        @RequestParam(defaultValue = "3") int size,
                        @RequestParam(defaultValue = "id,asc") String sort) {
            if (page < 0 || size < 1 || size > 50) throw new ResponseStatusException(HttpStatus.BAD_REQUEST);
            String[] parts = sort.split(",");
            Comparator<Item> order = switch (parts[0]) {
                case "id" -> Comparator.comparingLong(Item::id);
                case "name" -> Comparator.comparing(Item::name);
                case "price" -> Comparator.comparingInt(Item::price);
                default -> throw new ResponseStatusException(HttpStatus.BAD_REQUEST);
            };
            if (parts.length == 2 && parts[1].equals("desc")) order = order.reversed();
            List<Item> sorted = DATA.values().stream().sorted(order.thenComparingLong(Item::id)).toList();
            int from = (int) Math.min((long) page * size, sorted.size());
            return new Page<>(sorted.subList(from, Math.min(from + size, sorted.size())), page, size, sorted.size());
        }
    }

    @SpringBootConfiguration
    @EnableAutoConfiguration
    @Import(Items.class)
    static class Config {}

    public static void main(String[] args) throws Exception {
        // Thread safety: 8 threads add 500 items each. AtomicLong gives every item its own id, ConcurrentHashMap loses nothing.
        ExecutorService pool = Executors.newFixedThreadPool(8);
        for (int t = 0; t < 8; t++) {
            pool.submit(() -> { for (int i = 0; i < 500; i++) add("bulk", 1); });
        }
        pool.shutdown();
        pool.awaitTermination(3, TimeUnit.SECONDS);
        Set<Long> distinct = new HashSet<>(DATA.keySet());
        System.out.println("stored=" + DATA.size() + " distinctIds=" + distinct.size() + " lastId=" + IDS.get());

        DATA.clear();
        IDS.set(0);
        add("Tea", 250); add("Coffee", 400); add("Cocoa", 300); add("Juice", 150); add("Water", 100);

        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF")) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            for (String url : new String[] {"/items", "/items?size=2&page=1", "/items?sort=price,desc&size=2",
                    "/items?sort=name", "/items?page=9", "/items?sort=color", "/items?size=0"}) {
                MockHttpServletResponse response = mvc.perform(MockMvcRequestBuilders.get(url).accept(MediaType.APPLICATION_JSON)).andReturn().getResponse();
                System.out.println(String.format("%-30s %d %s", url, response.getStatus(), response.getContentAsString()).stripTrailing());
            }
        }
    }
}
```
```output
stored=4000 distinctIds=4000 lastId=4000
/items                         200 {"items":[{"id":1,"name":"Tea","price":250},{"id":2,"name":"Coffee","price":400},{"id":3,"name":"Cocoa","price":300}],"page":0,"size":3,"total":5}
/items?size=2&page=1           200 {"items":[{"id":3,"name":"Cocoa","price":300},{"id":4,"name":"Juice","price":150}],"page":1,"size":2,"total":5}
/items?sort=price,desc&size=2  200 {"items":[{"id":2,"name":"Coffee","price":400},{"id":3,"name":"Cocoa","price":300}],"page":0,"size":2,"total":5}
/items?sort=name               200 {"items":[{"id":3,"name":"Cocoa","price":300},{"id":2,"name":"Coffee","price":400},{"id":4,"name":"Juice","price":150}],"page":0,"size":3,"total":5}
/items?page=9                  200 {"items":[],"page":9,"size":3,"total":5}
/items?sort=color              400
/items?size=0                  400
```

Разберите три запроса. `size=2&page=1` - вторая страница по два элемента при сортировке по `id`. `sort=price,desc` - от дорогого к дешёвому. `page=9` вышла за конец данных: это не ошибка, а пустая страница с верным `total`. Неизвестное поле и `size=0` отвергнуты с 400.

<!-- section: mistakes -->
## Типичные ошибки

* **Отдавать наружу сущность.** Внутренние поля (себестоимость, хэши) попадут в JSON. Используйте отдельные DTO запроса и ответа.
* **Принимать `id` от клиента в теле `POST`.** Идентификатор назначает сервер; клиент, присылающий свой `id`, может перезаписать чужой ресурс.
* **Возвращать 200 при создании.** Правильно 201 и `Location`.
* **`HashMap` и `long++` в репозитории-одиночке.** Гонка данных: потерянные записи и одинаковые id. Нужны `ConcurrentHashMap` и `AtomicLong`.
* **Сортировка без вторичного ключа.** При равных значениях порядок элементов нестабилен, и страницы перекрываются. Добавляйте сравнение по `id`.
* **Не ограничить `size`.** Клиент может запросить весь набор данных или вызвать переполнение при вычислении смещения `page * size`. Проверяйте границы и считайте смещение в `long`.
* **Делать `null`-проверки в контроллере вместо исключений.** Бросайте доменное исключение и обрабатывайте его в одном месте.
* **Считать повторный `DELETE` ошибкой.** Идемпотентность говорит о состоянии, а не о коде: второй ответ 404 нормален.

<!-- section: summary -->
## Итоги

* Контроллер отвечает за HTTP, сервис за правила, репозиторий за хранение; Spring связывает слои через конструкторы.
* `POST` - 201 и `Location`, `PUT` - полная замена (200 или 404), `DELETE` - 204 или 404, ошибки - исключения плюс `@RestControllerAdvice`.
* Тело запроса и ответа - DTO-записи; правила описываются аннотациями проверки и включаются `@Valid`.
* Страницы и сортировку без Spring Data разбирают вручную с проверкой границ и вторичным ключом `id`.
* Репозиторий на `ConcurrentHashMap` и `AtomicLong` безопасен для параллельных запросов; `GET`, `PUT`, `DELETE` идемпотентны, `POST` нет.
* `ETag` с `If-None-Match` экономит передачу (304), `If-Match` защищает от потерянного обновления (412).
