package demo;

import java.time.Duration;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.stream.IntStream;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.boot.context.properties.bind.DefaultValue;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;
import org.springframework.core.env.Environment;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Component;
import org.springframework.util.unit.DataSize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@SpringBootApplication
public class App {
    // TODO 1: настройки должны читаться из свойств с префиксом app (app.greeting, app.max-items, app.timeout, app.max-upload,
    // app.tags, app.labels.*, app.beta-enabled), а не из жёстко заданного бина ниже.
    //  - пометьте запись @ConfigurationProperties(prefix = "app") и включите сканирование: @ConfigurationPropertiesScan на App;
    //  - значения по умолчанию задайте через @DefaultValue: greeting "Hello", maxItems 5, timeout 30s, maxUpload 1MB,
    //    а для tags и labels - пустые коллекции (@DefaultValue без значения);
    //  - удалите ручной бин Manual.
    public record AppProperties(
            String greeting,
            int maxItems,
            Duration timeout,
            DataSize maxUpload,
            List<String> tags,
            Map<String, String> labels,
            boolean betaEnabled) {}

    @Configuration
    static class Manual {
        @Bean
        AppProperties appProperties() {
            return new AppProperties("Hello", 5, Duration.ofSeconds(30), DataSize.ofMegabytes(1), List.of(), Map.of(), false);
        }
    }

    record ConfigView(String greeting, int maxItems, long timeoutSeconds, long maxUploadBytes,
                      List<String> tags, Map<String, String> labels, boolean betaEnabled) {}

    record ItemsView(int requested, int returned, List<Integer> items) {}

    interface Mailer {
        String name();
    }

    // TODO 3: ConsoleMailer нужен только в профиле dev (@Component + @Profile("dev")), SmtpMailer - во всех остальных (@Profile("!dev")).
    static class ConsoleMailer implements Mailer {
        public String name() {
            return "console";
        }
    }

    @Component
    static class SmtpMailer implements Mailer {
        public String name() {
            return "smtp";
        }
    }

    @RestController
    static class ConfigController {
        private final AppProperties properties;
        private final Mailer mailer;
        private final Environment environment;
        // TODO 2: подпись внизу берётся из свойства app.footer, а если его нет - "(none)": внедрите через @Value("${app.footer:(none)}").
        private final String footer = "(none)";

        ConfigController(AppProperties properties, Mailer mailer, Environment environment) {
            this.properties = properties;
            this.mailer = mailer;
            this.environment = environment;
        }

        @GetMapping("/config")
        ConfigView config() {
            return new ConfigView(properties.greeting(), properties.maxItems(), properties.timeout().toSeconds(),
                    properties.maxUpload().toBytes(), properties.tags(), new TreeMap<>(properties.labels()),
                    properties.betaEnabled());
        }

        @GetMapping("/greet/{name}")
        String greet(@PathVariable String name) {
            return properties.greeting() + ", " + name + "!";
        }

        @GetMapping("/items")
        ItemsView items(@RequestParam int count) {
            int returned = Math.max(0, Math.min(count, properties.maxItems()));
            return new ItemsView(count, returned, IntStream.rangeClosed(1, returned).boxed().toList());
        }

        @GetMapping("/beta")
        ResponseEntity<String> beta() {
            return properties.betaEnabled() ? ResponseEntity.ok("beta on") : ResponseEntity.notFound().build();
        }

        @GetMapping("/mailer")
        String mailer() {
            return mailer.name();
        }

        @GetMapping("/profiles")
        List<String> profiles() {
            return Arrays.asList(environment.getActiveProfiles());
        }

        @GetMapping("/footer")
        String footer() {
            return footer;
        }
    }
}
