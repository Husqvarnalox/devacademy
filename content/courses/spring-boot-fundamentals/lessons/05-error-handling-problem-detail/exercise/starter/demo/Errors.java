package demo;

import org.springframework.web.bind.annotation.RestControllerAdvice;

// TODO: превратите исключения сервиса в ответы ProblemDetail (таблица в условии задачи).
// Подсказка по структуре: класс-совет наследует ResponseEntityExceptionHandler, а каждое исключение
// получает свой метод с @ExceptionHandler, который возвращает ProblemDetail.
@RestControllerAdvice
class Errors {
}
