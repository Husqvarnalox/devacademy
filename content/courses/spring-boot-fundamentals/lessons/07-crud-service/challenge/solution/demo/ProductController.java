package demo;

import demo.App.PageResult;
import demo.App.Product;
import demo.App.ProductInput;
import jakarta.validation.Valid;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.HexFormat;
import org.springframework.web.context.request.WebRequest;
import tools.jackson.databind.json.JsonMapper;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/products")
class ProductController {
    private final ProductService service;
    private final JsonMapper json;

    ProductController(ProductService service, JsonMapper json) {
        this.service = service;
        this.json = json;
    }

    /** ETag: the first 16 hex digits of SHA-256 of the product's JSON text, in double quotes. */
    private String etag(Product product) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256").digest(json.writeValueAsString(product).getBytes(StandardCharsets.UTF_8));
            return "\"" + HexFormat.of().formatHex(digest).substring(0, 16) + "\"";
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }

    @GetMapping
    PageResult<Product> list(@RequestParam(defaultValue = "0") int page,
                             @RequestParam(defaultValue = "10") int size,
                             @RequestParam(defaultValue = "id,asc") String sort) {
        return service.list(page, size, sort);
    }

    @GetMapping("/{id}")
    ResponseEntity<Product> get(@PathVariable long id, WebRequest request) {
        Product product = service.get(id);
        String tag = etag(product);
        if (request.checkNotModified(tag)) return null;
        return ResponseEntity.ok().eTag(tag).body(product);
    }

    @PostMapping
    ResponseEntity<Product> create(@Valid @RequestBody ProductInput input) {
        Product product = service.create(input);
        return ResponseEntity.created(URI.create("/products/" + product.id())).body(product);
    }

    @PutMapping("/{id}")
    ResponseEntity<Product> replace(@PathVariable long id, @Valid @RequestBody ProductInput input, WebRequest request) {
        if (request.checkNotModified(etag(service.get(id)))) return null;
        Product product = service.replace(id, input);
        return ResponseEntity.ok().eTag(etag(product)).body(product);
    }

    @DeleteMapping("/{id}")
    ResponseEntity<Void> delete(@PathVariable long id) {
        service.delete(id);
        return ResponseEntity.noContent().build();
    }
}
