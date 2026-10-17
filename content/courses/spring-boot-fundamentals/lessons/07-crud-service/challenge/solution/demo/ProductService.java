package demo;

import demo.App.BadQueryException;
import demo.App.PageResult;
import demo.App.Product;
import demo.App.ProductInput;
import demo.App.ProductNotFoundException;
import java.util.Comparator;
import java.util.List;
import org.springframework.stereotype.Service;

@Service
class ProductService {
    private final ProductRepository repository;

    ProductService(ProductRepository repository) {
        this.repository = repository;
    }

    PageResult<Product> list(int page, int size, String sort) {
        if (page < 0) throw new BadQueryException("page must be >= 0");
        if (size < 1 || size > 100) throw new BadQueryException("size must be between 1 and 100");
        String[] parts = sort.split(",");
        Comparator<Product> order = switch (parts[0]) {
            case "id" -> Comparator.comparingLong(Product::id);
            case "name" -> Comparator.comparing(Product::name);
            case "price" -> Comparator.comparingInt(Product::price);
            default -> throw new BadQueryException("Unknown sort field: " + parts[0]);
        };
        if (parts.length > 2) throw new BadQueryException("Sort direction must be asc or desc");
        if (parts.length == 2) {
            if (parts[1].equals("desc")) order = order.reversed();
            else if (!parts[1].equals("asc")) throw new BadQueryException("Sort direction must be asc or desc");
        }
        List<Product> sorted = repository.findAll().stream()
                .sorted(order.thenComparingLong(Product::id))
                .toList();
        int from = (int) Math.min((long) page * size, sorted.size());
        int to = Math.min(from + size, sorted.size());
        return new PageResult<>(sorted.subList(from, to), page, size, sorted.size());
    }

    Product get(long id) {
        return repository.findById(id).orElseThrow(() -> new ProductNotFoundException(id));
    }

    Product create(ProductInput input) {
        return repository.save(new Product(0, input.name(), input.price()));
    }

    Product replace(long id, ProductInput input) {
        get(id);
        return repository.save(new Product(id, input.name(), input.price()));
    }

    void delete(long id) {
        if (!repository.deleteById(id)) throw new ProductNotFoundException(id);
    }
}
