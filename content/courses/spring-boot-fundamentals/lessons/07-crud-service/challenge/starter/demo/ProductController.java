package demo;

import demo.App.PageResult;
import demo.App.Product;
import demo.App.ProductInput;
import jakarta.validation.Valid;
import java.net.URI;
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

    ProductController(ProductService service) {
        this.service = service;
    }

    @GetMapping
    PageResult<Product> list(@RequestParam(defaultValue = "0") int page,
                             @RequestParam(defaultValue = "10") int size,
                             @RequestParam(defaultValue = "id,asc") String sort) {
        return service.list(page, size, sort);
    }

    // TODO (задача повышенной сложности): ETag для GET и PUT, If-None-Match -> 304, If-Match -> 412 (условие в описании задачи).
    @GetMapping("/{id}")
    Product get(@PathVariable long id) {
        return service.get(id);
    }

    @PostMapping
    ResponseEntity<Product> create(@Valid @RequestBody ProductInput input) {
        Product product = service.create(input);
        return ResponseEntity.created(URI.create("/products/" + product.id())).body(product);
    }

    @PutMapping("/{id}")
    Product replace(@PathVariable long id, @Valid @RequestBody ProductInput input) {
        return service.replace(id, input);
    }

    @DeleteMapping("/{id}")
    ResponseEntity<Void> delete(@PathVariable long id) {
        service.delete(id);
        return ResponseEntity.noContent().build();
    }
}
