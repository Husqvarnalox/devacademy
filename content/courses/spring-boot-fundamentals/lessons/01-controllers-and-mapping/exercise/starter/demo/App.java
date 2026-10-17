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

        // TODO 1: фильтр ?author=... без учёта регистра (параметр запроса попадает в аргумент метода через @RequestParam).
        @GetMapping("/books")
        List<Book> list() {
            return books;
        }

        // TODO 2: GET /books/{id}, только цифры в id (шаблон {id:\d+}): 200 с книгой или 404 (ResponseEntity.ok / notFound).
        // TODO 3: POST /books с телом NewBook (@RequestBody): 201, заголовок Location: /books/<id>, id = максимальный + 1.
    }
}
