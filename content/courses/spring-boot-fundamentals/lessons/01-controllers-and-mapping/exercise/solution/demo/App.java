package demo;

import java.util.ArrayList;
import java.util.List;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.net.URI;

@SpringBootApplication
public class App {
    public record Book(int id, String title, String author) {}

    public record NewBook(String title, String author) {}

    @RestController
    static class BookController {
        private final List<Book> books = new ArrayList<>(List.of(
                new Book(1, "Dune", "Herbert"), new Book(2, "Emma", "Austen"), new Book(3, "Persuasion", "Austen")));

        @GetMapping("/ping")
        String ping() {
            return "pong";
        }

        @GetMapping("/books")
        List<Book> list(@RequestParam(required = false) String author) {
            if (author == null) return books;
            return books.stream().filter(b -> b.author().equalsIgnoreCase(author)).toList();
        }

        @GetMapping("/books/{id:\\d+}")
        ResponseEntity<Book> get(@PathVariable int id) {
            return books.stream().filter(b -> b.id() == id).findFirst()
                    .map(ResponseEntity::ok)
                    .orElseGet(() -> ResponseEntity.notFound().build());
        }

        @PostMapping("/books")
        ResponseEntity<Book> create(@RequestBody NewBook input) {
            int id = books.stream().mapToInt(Book::id).max().orElse(0) + 1;
            Book book = new Book(id, input.title(), input.author());
            books.add(book);
            return ResponseEntity.created(URI.create("/books/" + id)).body(book);
        }
    }
}
