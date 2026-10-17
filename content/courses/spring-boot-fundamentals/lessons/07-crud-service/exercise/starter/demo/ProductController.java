package demo;

import demo.App.Product;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/products")
class ProductController {
    private final ProductService service;

    ProductController(ProductService service) {
        this.service = service;
    }

    @GetMapping("/{id}")
    Product get(@PathVariable long id) {
        return service.get(id);
    }

    // TODO 5: GET /products?page=0&size=10&sort=id,asc - страница PageResult<Product> (значения по умолчанию в @RequestParam(defaultValue = ...)).
    // TODO 6: POST /products - тело ProductInput с проверкой (@Valid @RequestBody), ответ 201, заголовок Location: /products/<id> и созданный товар.
    // TODO 7: PUT /products/{id} - тело ProductInput с проверкой, ответ 200 и сохранённый товар.
    // TODO 8: DELETE /products/{id} - ответ 204 без тела.
}
