package demo;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.server.ResponseStatusException;

// Ready-made service: do not edit. The error mapping you write lives in demo/Errors.java.
@SpringBootApplication
public class App {
    public record Account(long id, String owner, long balance, boolean closed) {}

    public static class AccountNotFoundException extends RuntimeException {
        private final long id;

        public AccountNotFoundException(long id) {
            super("no account " + id);
            this.id = id;
        }

        public long id() { return id; }
    }

    public static class AccountClosedException extends RuntimeException {
        private final long id;

        public AccountClosedException(long id) {
            super("account " + id + " closed");
            this.id = id;
        }

        public long id() { return id; }
    }

    public static class InsufficientFundsException extends RuntimeException {
        private final long id;
        private final long balance;
        private final long requested;

        public InsufficientFundsException(long id, long balance, long requested) {
            super("account " + id + ": " + balance + " < " + requested);
            this.id = id;
            this.balance = balance;
            this.requested = requested;
        }

        public long id() { return id; }
        public long balance() { return balance; }
        public long requested() { return requested; }
    }

    @RestController
    @RequestMapping("/accounts")
    static class AccountController {
        private final Map<Long, Account> accounts = new ConcurrentHashMap<>(Map.of(
                1L, new Account(1, "Ann", 100, false),
                2L, new Account(2, "Bob", 20, true)));

        @GetMapping("/{id}")
        Account get(@PathVariable long id) {
            Account account = accounts.get(id);
            if (account == null) throw new AccountNotFoundException(id);
            return account;
        }

        @PostMapping("/{id}/withdraw")
        Account withdraw(@PathVariable long id, @RequestParam long amount) {
            if (amount <= 0) throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Amount must be positive");
            Account account = get(id);
            if (account.closed()) throw new AccountClosedException(id);
            if (account.balance() < amount) throw new InsufficientFundsException(id, account.balance(), amount);
            Account updated = new Account(id, account.owner(), account.balance() - amount, false);
            accounts.put(id, updated);
            return updated;
        }

        @GetMapping("/{id}/statement")
        String statement(@PathVariable long id) {
            // Simulates an unexpected failure with an internal message that must never reach the client.
            throw new IllegalStateException("jdbc:postgresql://db.internal:5432/bank password=hunter2");
        }
    }
}
