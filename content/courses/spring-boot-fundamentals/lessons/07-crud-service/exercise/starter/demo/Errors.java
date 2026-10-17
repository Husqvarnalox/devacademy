package demo;

import java.net.URI;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

// Ready-made error mapping: do not edit. Your service throws the exceptions, this class turns them into ProblemDetail.
@RestControllerAdvice
class Errors extends ResponseEntityExceptionHandler {
    @ExceptionHandler(App.ProductNotFoundException.class)
    ProblemDetail notFound(App.ProductNotFoundException e) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, "Product " + e.id() + " does not exist");
        problem.setType(URI.create("https://example.com/problems/product-not-found"));
        problem.setTitle("Product not found");
        return problem;
    }

    @ExceptionHandler(App.BadQueryException.class)
    ProblemDetail badQuery(App.BadQueryException e) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.BAD_REQUEST, e.getMessage());
        problem.setTitle("Bad query");
        return problem;
    }
}
