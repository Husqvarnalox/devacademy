package demo;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;
import java.util.List;
import org.springframework.boot.autoconfigure.SpringBootApplication;

// Shared model and exceptions: ready-made, do not edit.
@SpringBootApplication
public class App {
    public record Product(long id, String name, int price) {}

    /** Request body of POST and PUT; the id always comes from the server. */
    public record ProductInput(@NotBlank String name, @Positive int price) {}

    /** One page of a listing. */
    public record PageResult<T>(List<T> items, int page, int size, long total) {}

    public static class ProductNotFoundException extends RuntimeException {
        private final long id;

        public ProductNotFoundException(long id) {
            super("no product " + id);
            this.id = id;
        }

        public long id() { return id; }
    }

    /** Invalid paging or sorting parameters; the message is written for the client. */
    public static class BadQueryException extends RuntimeException {
        public BadQueryException(String message) { super(message); }
    }
}
