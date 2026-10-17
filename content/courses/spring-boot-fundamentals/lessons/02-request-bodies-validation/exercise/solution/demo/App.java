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
    public record Address(@NotBlank(message = "city is required") String city) {}

    public record SignupRequest(
            @NotBlank(message = "username is required")
            @Pattern(regexp = "[a-z0-9]{3,12}", message = "username must be 3-12 lowercase letters or digits")
            String username,
            @NotBlank(message = "email is required")
            @Email(message = "email is invalid")
            String email,
            @NotNull(message = "age is required")
            @Min(value = 18, message = "age must be at least 18")
            @Max(value = 120, message = "age must be at most 120")
            Integer age,
            @Pattern(regexp = "[A-Z]{2}", message = "country must be two capital letters")
            String country,
            @Valid Address address) {}

    public record User(int id, String username, String email, int age, String country) {}

    @RestController
    static class UserController {
        private final List<User> users = new ArrayList<>();

        @PostMapping("/users")
        ResponseEntity<?> signup(@Valid @RequestBody SignupRequest request) {
            if (users.stream().anyMatch(u -> u.username().equals(request.username()))) {
                return ResponseEntity.status(HttpStatus.CONFLICT).body(Map.of("error", "username is taken"));
            }
            User user = new User(users.size() + 1, request.username(), request.email(), request.age(), request.country());
            users.add(user);
            return ResponseEntity.created(URI.create("/users/" + user.id())).body(user);
        }

        @GetMapping("/users")
        List<User> list(@RequestParam(defaultValue = "0") @Min(value = 0, message = "minAge must not be negative") int minAge) {
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
            Map<String, String> errors = new TreeMap<>();
            e.getBindingResult().getFieldErrors()
                    .forEach(error -> errors.merge(error.getField(), error.getDefaultMessage(),
                            (a, b) -> a.compareTo(b) <= 0 ? a : b));
            return ResponseEntity.badRequest().body(Map.of("errors", errors));
        }
    }
}
