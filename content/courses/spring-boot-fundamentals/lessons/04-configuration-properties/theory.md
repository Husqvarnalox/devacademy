<!-- section: simple -->
## Настройки вне кода

Одно и то же приложение работает на ноутбуке, в тестах и в продакшене, а различаются они адресами, лимитами, включёнными функциями. Такие значения нельзя вписывать в код: их выносят в **конфигурацию**, которую можно менять без пересборки. В Spring Boot конфигурация - это набор пар «ключ - значение» (`app.max-items=5`) из разных **источников**: файлов `application.properties` или `application.yaml`, переменных окружения, аргументов командной строки и других.

Прочитать значение можно двумя способами. Простой - аннотация `@Value("${ключ:запасное значение}")` на параметре конструктора. Основной - **типобезопасные свойства**: запись (`record`) со всеми настройками одной группы, к которой Spring Boot привязывает ключи с общим префиксом:

```java
@ConfigurationProperties(prefix = "app")
record AppProperties(
        @DefaultValue("Hello") String greeting,
        @DefaultValue("5") int maxItems,
        @DefaultValue("30s") Duration timeout) {}
```

Ключ `app.max-items` попадёт в `maxItems`, строка `30s` станет `Duration`, и всё это проверяется при запуске, а не при первом обращении. Для разных окружений есть **профили**: имя (`dev`, `prod`), которое включается свойством `spring.profiles.active`; под профиль подписываются бины (`@Profile("dev")`) и файлы (`application-dev.properties`).

В упражнениях этого курса файлов нет: каждый тест задаёт значения строками `@prop ключ=значение` и `@profile имя`, а драйвер передаёт их приложению как аргументы командной строки (`--ключ=значение`). Для приложения это такой же источник конфигурации, как любой другой.

<!-- section: precise -->
## Точные правила

**Источники и приоритет.** Если ключ задан в нескольких местах, побеждает источник с более высоким приоритетом. Упрощённо, от слабого к сильному: значения по умолчанию из кода (`SpringApplication.setDefaultProperties`), `application.properties`/`application.yaml`, профильные файлы (`application-dev.properties` сильнее общего), переменные окружения, аргументы командной строки. Поэтому аргумент `--app.greeting=Hi` всегда сильнее значения в файле. Полный список приведён в документации Spring Boot.

**Гибкая привязка (relaxed binding).** Одно и то же свойство можно записать по-разному: `app.max-items` (рекомендуется для файлов), `app.maxItems`, `app.max_items` и `APP_MAX_ITEMS` (для переменной окружения) привязываются к компоненту `maxItems`. В `@Value` пишите ключ строго в канонической форме kebab-case: такой гибкости он не обеспечивает.

**Привязка к записи.** Параметры конструктора получают значения по имени. Если ключа нет, примитив получает значение по умолчанию (`0`, `false`), объект - `null`. Задать запасное значение можно `@DefaultValue("5")`. Коллекции и карты при отсутствии ключа тоже `null`, если не поставить `@DefaultValue` без аргумента: тогда получается пустая коллекция. Поддерживаются `Duration` (`30s`, `2m`, `PT1H`), `DataSize` (`512KB`, `5MB`), списки (`a,b,c` или `app.tags[0]=a`), карты (`app.labels.env=prod`).

**Как включить привязку.** Класс со свойствами должен стать бином. `@ConfigurationPropertiesScan` на классе приложения находит все такие классы сам, `@EnableConfigurationProperties(AppProperties.class)` регистрирует их явно. В обоих случаях можно внедрять `AppProperties` в конструкторы, как любой бин.

**Проверка при запуске.** `@Validated` на классе свойств и ограничения `jakarta.validation` на компонентах проверяют значения при привязке. Неверная конфигурация **останавливает запуск**: приложение лучше не стартует, чем работает с мусором. Привязка к полю с неподходящим типом (`app.max-items=abc`) падает так же.

**Профили.** `@Profile("dev")` включает бин только в профиле `dev`; `@Profile("!dev")` - во всех остальных, `@Profile("prod & metrics")` - только если активны оба профиля. Включают профили свойством `spring.profiles.active=dev,metrics`. Профильная **группа** `spring.profiles.group.prod=prod,metrics` включает набор профилей одним именем. Бин без `@Profile` создаётся всегда. Если ни один профиль не включён, приложение работает с профилем `default`, но `getActiveProfiles()` вернёт пустой массив.

**Условия по свойству.** `@ConditionalOnProperty(name = "app.beta-enabled", havingValue = "true")` создаёт бин, только если свойство имеет нужное значение: так включают и выключают функции флагом.

<!-- section: examples -->
## Примеры

Привязка к записи, преобразование типов, гибкая запись ключей, `@Value` с запасным значением и приоритет аргумента над значением по умолчанию. Контекст запускается три раза с разными аргументами командной строки; значение по умолчанию задано в коде и слабее любого аргумента:

```java run
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Configuration;
import org.springframework.stereotype.Component;
import org.springframework.util.unit.DataSize;

public class Main {
    @ConfigurationProperties(prefix = "app")
    record AppProperties(
            @DefaultValue("Hello") String greeting,
            @DefaultValue("5") int maxItems,
            @DefaultValue("30s") Duration timeout,
            @DefaultValue("1MB") DataSize maxUpload,
            List<String> tags,
            @DefaultValue Map<String, String> labels) {}

    @Component
    static class Report {
        private final AppProperties properties;
        private final String footer;

        Report(AppProperties properties, @Value("${app.footer:(none)}") String footer) {
            this.properties = properties;
            this.footer = footer;
        }

        String describe() {
            return properties.greeting() + " | max=" + properties.maxItems() + " | timeout=" + properties.timeout().toSeconds() + "s"
                    + " | upload=" + properties.maxUpload().toBytes() + "B | tags=" + properties.tags()
                    + " | labels=" + new TreeMap<>(properties.labels()) + " | footer=" + footer;
        }
    }

    @Configuration
    @EnableConfigurationProperties(AppProperties.class)   // registers the record as a bean and binds it
    static class Config {}

    static void run(String title, String... args) {
        String[] all = new String[args.length + 1];
        System.arraycopy(args, 0, all, 0, args.length);
        all[args.length] = "--logging.level.root=OFF";
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class, Report.class)
                .web(WebApplicationType.NONE)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .properties("app.greeting=Default from code")   // lowest priority: default properties
                .run(all)) {
            System.out.println(title + ": " + context.getBean(Report.class).describe());
        }
    }

    public static void main(String[] args) {
        run("defaults");
        run("kebab", "--app.max-items=2", "--app.timeout=2m", "--app.max-upload=5MB", "--app.tags=a,b,c",
                "--app.labels.env=prod", "--app.labels.team=core", "--app.footer=bye");
        run("camel", "--app.maxItems=7", "--app.timeout=PT1H", "--app.max-upload=2KB", "--app.tags[0]=x", "--app.tags[1]=y",
                "--app.greeting=Hi");
    }
}
```
```output
defaults: Default from code | max=5 | timeout=30s | upload=1048576B | tags=null | labels={} | footer=(none)
kebab: Default from code | max=2 | timeout=120s | upload=5242880B | tags=[a, b, c] | labels={env=prod, team=core} | footer=bye
camel: Hi | max=7 | timeout=3600s | upload=2048B | tags=[x, y] | labels={} | footer=(none)
```

Первый запуск без аргументов: `greeting` берётся из значения по умолчанию в коде, остальное - из `@DefaultValue`. Обратите внимание на `tags=null`: у списка нет `@DefaultValue`, и без ключа он `null` (а у `labels` с `@DefaultValue` - пустая карта). Второй запуск записывает ключи в kebab-case, третий - `maxItems` и список через индексы, а `--app.greeting=Hi` перекрыл значение из кода.

Проверка свойств при запуске. Неверное значение не доходит до работы приложения: контекст не стартует, а в причине ошибки лежит список нарушений:

```java run
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;
import org.springframework.boot.context.properties.bind.validation.BindValidationException;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Configuration;
import org.springframework.validation.annotation.Validated;

public class Main {
    @Validated
    @ConfigurationProperties(prefix = "mail")
    record MailProperties(
            @NotBlank(message = "host is required") String host,
            @DefaultValue("25") @Min(value = 1, message = "port must be 1..65535") @Max(value = 65535, message = "port must be 1..65535") int port) {}

    @Configuration
    @EnableConfigurationProperties(MailProperties.class)
    static class Config {}

    static void start(String title, String... args) {
        String[] all = new String[args.length + 1];
        System.arraycopy(args, 0, all, 0, args.length);
        all[args.length] = "--logging.level.root=OFF";
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.NONE).bannerMode(Banner.Mode.OFF).logStartupInfo(false).run(all)) {
            MailProperties mail = context.getBean(MailProperties.class);
            System.out.println(title + ": started, " + mail.host() + ":" + mail.port());
        } catch (Exception e) {
            Throwable cause = e;
            while (cause != null && !(cause instanceof BindValidationException)) cause = cause.getCause();
            System.out.println(title + ": failed, " + e.getClass().getSimpleName());
            if (cause != null) {
                ((BindValidationException) cause).getValidationErrors().getAllErrors()
                        .forEach(error -> System.out.println("  " + error.getObjectName() + " -> " + error.getDefaultMessage()));
            }
        }
    }

    public static void main(String[] args) {
        start("valid", "--mail.host=smtp.example.org", "--mail.port=587");
        start("defaults", "--mail.host=smtp.example.org");
        start("bad port", "--mail.host=smtp.example.org", "--mail.port=70000");
        start("no host");
    }
}
```
```output
valid: started, smtp.example.org:587
defaults: started, smtp.example.org:25
bad port: failed, ConfigurationPropertiesBindException
  mail -> port must be 1..65535
no host: failed, ConfigurationPropertiesBindException
  mail -> host is required
```

Профили и условные бины. Тот же набор классов запускается с разными профилями и флагом `app.beta-enabled`:

```java run
import java.util.Arrays;
import java.util.List;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Import;
import org.springframework.context.annotation.Profile;
import org.springframework.stereotype.Component;

public class Main {
    interface Mailer {
        String name();
    }

    @Component
    @Profile("dev")
    static class ConsoleMailer implements Mailer {
        public String name() { return "console"; }
    }

    @Component
    @Profile("!dev")                      // every profile except dev (including "no profile at all")
    static class SmtpMailer implements Mailer {
        public String name() { return "smtp"; }
    }

    @Component
    @Profile("prod & metrics")            // both must be active
    static class MetricsExporter {}

    @Component
    @ConditionalOnProperty(name = "app.beta-enabled", havingValue = "true")
    static class BetaFeature {}

    @Configuration
    @Import({ConsoleMailer.class, SmtpMailer.class, MetricsExporter.class, BetaFeature.class})
    static class Config {}

    static void run(String title, String... args) {
        String[] all = Arrays.copyOf(args, args.length + 1);
        all[args.length] = "--logging.level.root=OFF";
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                .web(WebApplicationType.NONE).bannerMode(Banner.Mode.OFF).logStartupInfo(false).run(all)) {
            System.out.println(title + ": profiles=" + Arrays.toString(context.getEnvironment().getActiveProfiles())
                    + " mailer=" + context.getBean(Mailer.class).name()
                    + " metrics=" + (context.getBeanProvider(MetricsExporter.class).getIfAvailable() != null)
                    + " beta=" + (context.getBeanProvider(BetaFeature.class).getIfAvailable() != null));
        }
    }

    public static void main(String[] args) {
        run("no profile");
        run("dev", "--spring.profiles.active=dev", "--app.beta-enabled=true");
        run("prod", "--spring.profiles.active=prod");
        run("prod group", "--spring.profiles.active=prod", "--spring.profiles.group.prod=prod,metrics", "--app.beta-enabled=false");
    }
}
```
```output
no profile: profiles=[] mailer=smtp metrics=false beta=false
dev: profiles=[dev] mailer=console metrics=false beta=true
prod: profiles=[prod] mailer=smtp metrics=false beta=false
prod group: profiles=[prod, metrics] mailer=smtp metrics=true beta=false
```

Без профиля сработало `@Profile("!dev")`; в `dev` подключился `ConsoleMailer`. `MetricsExporter` появился только тогда, когда группа `prod` включила и `prod`, и `metrics`. Бин с `@ConditionalOnProperty` существует только при `app.beta-enabled=true`. В ASP.NET Core ту же роль играют `appsettings.{Environment}.json`, `IOptions<T>` и `IConfiguration` (см. [конфигурацию и Options](lesson:csharp.aspnetcore.configuration-options)): у Spring Boot привязка и проверка делаются классом свойств, а окружение выбирается профилем.

<!-- section: mistakes -->
## Типичные ошибки

* **Сделать настройки через `@Value` для целой группы.** Десяток `@Value` в разных классах не проверить и не найти. Сгруппируйте их в запись с `@ConfigurationProperties`.
* **Забыть включить привязку.** Класс с `@ConfigurationProperties` без `@ConfigurationPropertiesScan` или `@EnableConfigurationProperties` не станет бином, и внедрение упадёт при запуске.
* **Ждать пустую коллекцию вместо `null`.** Без `@DefaultValue` список и карта при отсутствии ключа равны `null`; `properties.tags().size()` бросит `NullPointerException`.
* **Писать в файле `app.maxItems` и `@Value("${app.maxItems}")`.** Для файлов и `@Value` пользуйтесь kebab-case (`app.max-items`).
* **Хранить секреты в `application.properties` в репозитории.** Пароли и ключи передают переменными окружения или хранилищем секретов; профиль не защищает от утечки.
* **Строить логику на `if (profile.equals("prod"))`.** Выбор реализации делайте бинами с `@Profile`, а различия значений - свойствами.
* **Надеяться, что опечатка в ключе будет замечена.** Неизвестный ключ по умолчанию не ошибка: значение молча останется запасным. Проверяйте важные ключи ограничениями `@Validated`.

<!-- section: summary -->
## Итоги

* Конфигурация - пары «ключ - значение» из нескольких источников; аргументы командной строки и переменные окружения сильнее файлов и значений по умолчанию.
* `@ConfigurationProperties(prefix = ...)` на записи привязывает группу свойств с гибкими именами и преобразованием типов (`Duration`, `DataSize`, списки, карты); `@DefaultValue` задаёт запасные значения.
* Привязку включают `@ConfigurationPropertiesScan` или `@EnableConfigurationProperties`; `@Validated` проверяет значения, и неверная конфигурация останавливает запуск.
* `@Value("${ключ:запасное}")` годится для одиночного значения.
* Профили (`@Profile`, `spring.profiles.active`, группы) выбирают бины под окружение, `@ConditionalOnProperty` включает функции флагом.

Дальше: [обработка ошибок и ProblemDetail](lesson:java.spring.error-handling-problem-detail).
