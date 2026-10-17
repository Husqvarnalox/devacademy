package demo;

import java.net.URI;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

@RestControllerAdvice
class Errors extends ResponseEntityExceptionHandler {
    @ExceptionHandler(App.AccountNotFoundException.class)
    ProblemDetail notFound(App.AccountNotFoundException e) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, "Account " + e.id() + " does not exist");
        problem.setType(URI.create("https://example.com/problems/account-not-found"));
        problem.setTitle("Account not found");
        return problem;
    }

    @ExceptionHandler(App.AccountClosedException.class)
    ProblemDetail closed(App.AccountClosedException e) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.CONFLICT, "Account " + e.id() + " is closed");
        problem.setType(URI.create("https://example.com/problems/account-closed"));
        problem.setTitle("Account closed");
        return problem;
    }

    @ExceptionHandler(App.InsufficientFundsException.class)
    ProblemDetail insufficient(App.InsufficientFundsException e) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.UNPROCESSABLE_CONTENT,
                "Balance " + e.balance() + " is less than requested " + e.requested());
        problem.setType(URI.create("https://example.com/problems/insufficient-funds"));
        problem.setTitle("Insufficient funds");
        problem.setProperty("balance", e.balance());
        problem.setProperty("requested", e.requested());
        return problem;
    }

    @ExceptionHandler(Exception.class)
    ProblemDetail unexpected(Exception e) {
        // The message and stack trace stay on the server; the client gets a fixed text.
        return ProblemDetail.forStatusAndDetail(HttpStatus.INTERNAL_SERVER_ERROR, "Unexpected error");
    }
}
