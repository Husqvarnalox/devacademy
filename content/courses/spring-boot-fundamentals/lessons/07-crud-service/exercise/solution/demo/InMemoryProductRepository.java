package demo;

import demo.App.Product;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;
import org.springframework.stereotype.Repository;

// Ready-made in-memory storage: ConcurrentHashMap for the data, AtomicLong for ids, so it is safe for concurrent requests.
@Repository
class InMemoryProductRepository implements ProductRepository {
    private final ConcurrentHashMap<Long, Product> products = new ConcurrentHashMap<>();
    private final AtomicLong lastId = new AtomicLong();

    InMemoryProductRepository() {
        save(new Product(0, "Tea", 250));
        save(new Product(0, "Coffee", 400));
        save(new Product(0, "Cocoa", 300));
        save(new Product(0, "Juice", 150));
        save(new Product(0, "Water", 100));
    }

    @Override
    public List<Product> findAll() {
        return new ArrayList<>(products.values());
    }

    @Override
    public Optional<Product> findById(long id) {
        return Optional.ofNullable(products.get(id));
    }

    @Override
    public Product save(Product product) {
        long id = product.id() == 0 ? lastId.incrementAndGet() : product.id();
        Product stored = new Product(id, product.name(), product.price());
        products.put(id, stored);
        return stored;
    }

    @Override
    public boolean deleteById(long id) {
        return products.remove(id) != null;
    }
}
