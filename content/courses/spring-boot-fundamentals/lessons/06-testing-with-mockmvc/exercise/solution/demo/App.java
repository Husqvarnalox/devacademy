package demo;

import java.net.URI;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CopyOnWriteArrayList;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

// The application under test: do not edit. Property app.variant selects "good" or one of the deliberate bugs.
@SpringBootApplication
public class App {
    public record Product(int id, String name, int price) {}

    public record NewProduct(String name, int price) {}

    @RestController
    @RequestMapping("/products")
    static class ProductController {
        private final String variant;
        private final List<Product> products = new CopyOnWriteArrayList<>(List.of(
                new Product(1, "Tea", 250), new Product(2, "Coffee", 400)));

        ProductController(@Value("${app.variant:good}") String variant) {
            this.variant = variant;
        }

        @GetMapping
        List<Product> list() {
            return products;
        }

        @GetMapping("/{id}")
        ResponseEntity<?> one(@PathVariable int id) {
            Product product = products.stream().filter(p -> p.id() == id).findFirst().orElse(null);
            if (product == null) {
                return variant.equals("bug-notfound") ? ResponseEntity.ok().build() : ResponseEntity.notFound().build();
            }
            if (variant.equals("bug-field")) {
                Map<String, Object> partial = new LinkedHashMap<>();
                partial.put("id", product.id());
                partial.put("name", product.name());
                return ResponseEntity.ok(partial);
            }
            if (variant.equals("bug-type")) {
                String json = "{\"id\":" + product.id() + ",\"name\":\"" + product.name() + "\",\"price\":" + product.price() + "}";
                return ResponseEntity.ok().contentType(MediaType.TEXT_PLAIN).body(json);
            }
            return ResponseEntity.ok(product);
        }

        @PostMapping
        ResponseEntity<?> create(@RequestBody NewProduct input) {
            boolean invalid = input.name() == null || input.name().isBlank() || input.price() <= 0;
            if (invalid && !variant.equals("bug-validation")) return ResponseEntity.badRequest().build();
            Product product = new Product(products.size() + 1, input.name(), input.price());
            products.add(product);
            if (variant.equals("bug-status")) return ResponseEntity.ok(product);
            return ResponseEntity.created(URI.create("/products/" + product.id())).body(product);
        }
    }
}
