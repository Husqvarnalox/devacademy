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
@ConfigurationPropertiesScan
public class App {
    // Настройки приложения: префикс app, привязка по имени компонента записи (maxItems <-> app.max-items).
    @ConfigurationProperties(prefix = "app")
    public record AppProperties(
            @DefaultValue("Hello") String greeting,
            @DefaultValue("5") int maxItems,
            @DefaultValue("30s") Duration timeout,
            @DefaultValue("1MB") DataSize maxUpload,
            @DefaultValue List<String> tags,
            @DefaultValue Map<String, String> labels,
            boolean betaEnabled) {}

    record ConfigView(String greeting, int maxItems, long timeoutSeconds, long maxUploadBytes,
                      List<String> tags, Map<String, String> labels, boolean betaEnabled) {}

    record ItemsView(int requested, int returned, List<Integer> items) {}

    interface Mailer {
        String name();
    }

    @Component
    @Profile("dev")
    static class ConsoleMailer implements Mailer {
        public String name() {
            return "console";
        }
    }

    @Component
    @Profile("!dev")
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
        private final String footer;

        ConfigController(AppProperties properties, Mailer mailer, Environment environment,
                         @Value("${app.footer:(none)}") String footer) {
            this.properties = properties;
            this.mailer = mailer;
            this.environment = environment;
            this.footer = footer;
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
