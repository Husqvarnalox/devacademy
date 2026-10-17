package demo;

import java.util.List;
import java.util.Optional;
import demo.App.Product;

// Ready-made storage contract: do not edit.
interface ProductRepository {
    /** A snapshot of all products in no particular order. */
    List<Product> findAll();

    Optional<Product> findById(long id);

    /** With id 0 assigns the next free id; otherwise stores the product under its id (insert or replace). Returns the stored product. */
    Product save(Product product);

    /** Returns true if the product existed. */
    boolean deleteById(long id);
}
