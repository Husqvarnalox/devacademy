package demo;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@SpringBootApplication
public class App {
    // TODO 1 (продолжение): city обязателен, сообщение "city is required".
    public record Address(String city) {}

    public record SignupRequest(
            // TODO 1: ограничения. username: обязателен (@NotBlank) и подходит под [a-z0-9]{3,12}; email: обязателен и @Email;
            // age: @NotNull, от 18 (@Min) до 120 (@Max); country (необязательно): @Pattern [A-Z]{2}; address: вложенный объект (@Valid).
            // Тексты сообщений - в условии задания (параметр message = "...").
            String username,
            String email,
            Integer age,
            String country,
            Address address) {}

    public record User(int id, String username, String email, int age, String country) {}

    @RestController
    static class UserController {
        private final List<User> users = new ArrayList<>();

        @PostMapping("/users")
        ResponseEntity<?> signup(/* TODO 2: включите проверку */ @RequestBody SignupRequest request) {
            if (users.stream().anyMatch(u -> u.username().equals(request.username()))) {
                return ResponseEntity.status(HttpStatus.CONFLICT).body(Map.of("error", "username is taken"));
            }
            User user = new User(users.size() + 1, request.username(), request.email(), request.age(), request.country());
            users.add(user);
            return ResponseEntity.created(URI.create("/users/" + user.id())).body(user);
        }

        @GetMapping("/users")
        List<User> list(@RequestParam(defaultValue = "0") /* TODO 3: minAge не меньше 0, сообщение "minAge must not be negative" */ int minAge) {
            return users.stream().filter(u -> u.age() >= minAge).toList();
        }

        @GetMapping("/users/{id:\\d+}")
        ResponseEntity<User> get(@PathVariable int id) {
            return users.stream().filter(u -> u.id() == id).findFirst()
                    .map(ResponseEntity::ok)
                    .orElseGet(() -> ResponseEntity.notFound().build());
        }
    }

    @RestControllerAdvice
    static class ValidationErrors {
        @ExceptionHandler(MethodArgumentNotValidException.class)
        ResponseEntity<Map<String, Object>> invalid(MethodArgumentNotValidException e) {
            // TODO 4: соберите поле -> сообщение из e.getBindingResult().getFieldErrors() в TreeMap (порядок ключей по алфавиту);
            // если у поля несколько нарушений, оставьте сообщение, которое идёт первым по алфавиту.
            Map<String, String> errors = new TreeMap<>();
            return ResponseEntity.badRequest().body(Map.of("errors", errors));
        }
    }
}
