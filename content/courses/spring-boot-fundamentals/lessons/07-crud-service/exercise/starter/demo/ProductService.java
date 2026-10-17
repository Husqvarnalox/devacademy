package demo;

import demo.App.PageResult;
import demo.App.Product;
import demo.App.ProductInput;
import demo.App.ProductNotFoundException;
import org.springframework.stereotype.Service;

@Service
class ProductService {
    private final ProductRepository repository;

    ProductService(ProductRepository repository) {
        this.repository = repository;
    }

    // TODO 1: постраничный список. Проверки: page >= 0, size от 1 до 100, поле sort - id, name или price, направление asc (по умолчанию) или desc;
    // при ошибке бросайте BadQueryException с понятным сообщением. Сортировка: по полю, при равенстве по id по возрастанию.
    PageResult<Product> list(int page, int size, String sort) {
        throw new UnsupportedOperationException("TODO");
    }

    Product get(long id) {
        return repository.findById(id).orElseThrow(() -> new ProductNotFoundException(id));
    }

    // TODO 2: сохранить новый товар (id назначает репозиторий: передайте 0).
    Product create(ProductInput input) {
        throw new UnsupportedOperationException("TODO");
    }

    // TODO 3: заменить товар целиком; если товара нет - ProductNotFoundException.
    Product replace(long id, ProductInput input) {
        throw new UnsupportedOperationException("TODO");
    }

    // TODO 4: удалить товар; если товара нет - ProductNotFoundException.
    void delete(long id) {
        throw new UnsupportedOperationException("TODO");
    }
}
