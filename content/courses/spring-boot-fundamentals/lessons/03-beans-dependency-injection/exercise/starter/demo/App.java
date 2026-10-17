package demo;

import jakarta.annotation.PostConstruct;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Primary;
import org.springframework.context.annotation.Scope;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Component;
import org.springframework.stereotype.Repository;
import org.springframework.stereotype.Service;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.context.annotation.RequestScope;
import org.springframework.web.server.ResponseStatusException;

@SpringBootApplication
public class App {
    // ---- Скидки: несколько реализаций одного интерфейса ----
    interface DiscountPolicy {
        int apply(int cents);
    }

    @Component("standard")
    @Primary
    static class StandardPolicy implements DiscountPolicy {
        public int apply(int cents) {
            return cents;
        }
    }

    // TODO 1: сделайте класс бином с именем "seasonal" (@Component("seasonal")); без этого он не попадает в контекст.
    static class SeasonalPolicy implements DiscountPolicy {
        public int apply(int cents) {
            return cents * 90 / 100;
        }
    }

    // TODO 1: бин с именем "bulk".
    static class BulkPolicy implements DiscountPolicy {
        public int apply(int cents) {
            return cents * 80 / 100;
        }
    }

    // ---- Класс "из библиотеки": на нём нельзя поставить @Component ----
    static final class TaxCalculator {
        private final int percent;

        TaxCalculator(int percent) {
            this.percent = percent;
        }

        int tax(int cents) {
            return cents * percent / 100;
        }
    }

    @Configuration
    static class ShopConfig {
        @Bean
        TaxCalculator taxCalculator() {
            // TODO 2: ставка налога должна быть 20 процентов.
            return new TaxCalculator(0);
        }
    }

    // ---- Каталог с инициализацией после создания ----
    @Repository
    static class Catalog {
        private final Map<String, Integer> prices = new HashMap<>();

        // TODO 3: этот метод должен вызываться контейнером сразу после создания бина (@PostConstruct).
        void load() {
            prices.put("pen", 100);
            prices.put("book", 1250);
            prices.put("lamp", 3999);
        }

        Optional<Integer> price(String sku) {
            return Optional.ofNullable(prices.get(sku));
        }
    }

    record Quote(String sku, int qty, int subtotal, int discounted, int tax, int total, String policy) {}

    record Promo(String sku, int price, int promo) {}

    @Service
    static class PricingService {
        private final Catalog catalog;
        private final Map<String, DiscountPolicy> policies;
        private final TaxCalculator taxes;

        PricingService(Catalog catalog, Map<String, DiscountPolicy> policies, TaxCalculator taxes) {
            this.catalog = catalog;
            this.policies = policies;
            this.taxes = taxes;
        }

        Quote quote(String sku, int qty, String policyName) {
            DiscountPolicy policy = policies.get(policyName);
            if (policy == null) throw new ResponseStatusException(HttpStatus.BAD_REQUEST);
            int price = catalog.price(sku).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
            int subtotal = price * qty;
            int discounted = policy.apply(subtotal);
            int tax = taxes.tax(discounted);
            return new Quote(sku, qty, subtotal, discounted, tax, discounted + tax, policyName);
        }
    }

    @Service
    static class PromoService {
        private final Catalog catalog;
        private final DiscountPolicy policy;

        // TODO 4: промо считается по политике "seasonal", а не по основной (@Primary): уточните параметр через @Qualifier.
        PromoService(Catalog catalog, DiscountPolicy policy) {
            this.catalog = catalog;
            this.policy = policy;
        }

        Promo promo(String sku) {
            int price = catalog.price(sku).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
            return new Promo(sku, price, policy.apply(price));
        }
    }

    @RestController
    static class ShopController {
        private final PricingService pricing;
        private final PromoService promos;

        ShopController(PricingService pricing, PromoService promos) {
            this.pricing = pricing;
            this.promos = promos;
        }

        @GetMapping("/quote")
        Quote quote(@RequestParam String sku, @RequestParam int qty, @RequestParam(defaultValue = "standard") String policy) {
            return pricing.quote(sku, qty, policy);
        }

        @GetMapping("/promo")
        Promo promo(@RequestParam String sku) {
            return promos.promo(sku);
        }
    }

    // ---- Уведомления: порядок в List<Notifier> задаёт @Order ----
    interface Notifier {
        String send(String user);
    }

    // TODO 5: порядок в списке задаёт @Order: push (1), sms (2), email (3).
    @Component
    static class EmailNotifier implements Notifier {
        public String send(String user) {
            return "email:" + user;
        }
    }

    @Component
    static class PushNotifier implements Notifier {
        public String send(String user) {
            return "push:" + user;
        }
    }

    @Component
    static class SmsNotifier implements Notifier {
        public String send(String user) {
            return "sms:" + user;
        }
    }

    @RestController
    static class NotifyController {
        private final List<Notifier> notifiers;

        NotifyController(List<Notifier> notifiers) {
            this.notifiers = notifiers;
        }

        @GetMapping("/notify/{user}")
        List<String> notify(@PathVariable String user) {
            return notifiers.stream().map(n -> n.send(user)).toList();
        }
    }

    // ---- Билеты: у каждой очереди свой счётчик (prototype) ----
    // TODO 6: каждая очередь должна получать свой счётчик: бин не singleton, а prototype (@Scope("prototype")).
    @Component
    static class TicketCounter {
        private final AtomicInteger last = new AtomicInteger();

        int next() {
            return last.incrementAndGet();
        }
    }

    @Service
    static class QueueService {
        private final ObjectProvider<TicketCounter> counters;
        private final Map<String, TicketCounter> byQueue = new ConcurrentHashMap<>();

        QueueService(ObjectProvider<TicketCounter> counters) {
            this.counters = counters;
        }

        int next(String queue) {
            return byQueue.computeIfAbsent(queue, name -> counters.getObject()).next();
        }
    }

    record Ticket(String queue, int number) {}

    @RestController
    static class QueueController {
        private final QueueService queues;

        QueueController(QueueService queues) {
            this.queues = queues;
        }

        @PostMapping("/queues/{name}/tickets")
        ResponseEntity<Ticket> take(@PathVariable String name) {
            return ResponseEntity.status(HttpStatus.CREATED).body(new Ticket(name, queues.next(name)));
        }
    }

    // ---- След запроса: один объект на запрос (request scope) ----
    // TODO 7: на каждый HTTP-запрос нужен новый объект: область видимости запроса (@RequestScope).
    @Component
    static class RequestTrace {
        private final List<String> steps = new ArrayList<>();

        void add(String step) {
            steps.add(step);
        }

        List<String> steps() {
            return List.copyOf(steps);
        }
    }

    @Service
    static class AuditService {
        private final RequestTrace trace;

        AuditService(RequestTrace trace) {
            this.trace = trace;
        }

        void record(String what) {
            trace.add("audit:" + what);
        }
    }

    @Service
    static class BillingService {
        private final RequestTrace trace;

        BillingService(RequestTrace trace) {
            this.trace = trace;
        }

        void charge(String what) {
            trace.add("billing:" + what);
        }
    }

    @RestController
    static class TraceController {
        private final AuditService audit;
        private final BillingService billing;
        private final RequestTrace trace;

        TraceController(AuditService audit, BillingService billing, RequestTrace trace) {
            this.audit = audit;
            this.billing = billing;
            this.trace = trace;
        }

        @GetMapping("/trace")
        List<String> trace() {
            audit.record("start");
            billing.charge("fee");
            return trace.steps();
        }
    }
}
