<!-- section: simple -->
## Что проверяет эта проверка

Обработка ошибок на значениях: `Result<T, E>`, оператор `?` в функциях, возвращающих `Result` или `Option`, собственный `enum` ошибки, реализации `Display` и `From`, преобразование через `map_err`, сбор итератора `Result` в `Result<Vec<_>, _>` и выбор между `Result` и паникой.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Обработка ошибок: Result, ? и свои типы ошибок](lesson:rust.fundamentals.error-handling).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```rust run
use std::fmt;
use std::num::ParseIntError;

#[derive(Debug)]
enum AgeError {
    Parse(ParseIntError),
    TooOld(u32),
}

impl fmt::Display for AgeError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match self {
            AgeError::Parse(e) => write!(f, "not a number: {}", e),
            AgeError::TooOld(n) => write!(f, "{} is too old", n),
        }
    }
}

impl From<ParseIntError> for AgeError {
    fn from(e: ParseIntError) -> Self {
        AgeError::Parse(e)
    }
}

fn read_age(text: &str) -> Result<u32, AgeError> {
    let age: u32 = text.trim().parse()?;
    if age > 150 {
        return Err(AgeError::TooOld(age));
    }
    Ok(age)
}

fn main() {
    for text in ["42", " 7 ", "abc", "-3", "200"] {
        match read_age(text) {
            Ok(age) => println!("age {}", age),
            Err(e) => println!("error: {}", e),
        }
    }
    let all: Result<Vec<u32>, _> = "1 2 3".split(' ').map(|s| s.parse::<u32>()).collect();
    let bad: Result<Vec<u32>, _> = "1 x 3".split(' ').map(|s| s.parse::<u32>()).collect();
    println!("{:?} {}", all, bad.is_err());
}
```

```output
age 42
age 7
error: not a number: invalid digit found in string
error: not a number: invalid digit found in string
error: 200 is too old
Ok([1, 2, 3]) true
```

Оператор `?` разворачивает `Ok`, а при `Err` досрочно возвращает ошибку из функции, предварительно преобразуя её через `From`. Поэтому в `read_age` вызов `parse()?` даёт `AgeError::Parse` сам, без `match`. Реализация `Display` задаёт текст, который увидит пользователь, а `Debug` остаётся для разработчика. Метод `collect` умеет собирать итератор `Result` в `Result<Vec<_>, _>`: он останавливается на первой ошибке. Паника допустима там, где ошибка означает баг программиста, а не плохой ввод.

<!-- section: precise -->
## Что нужно уметь

* возвращать `Result<T, E>` из функции и обрабатывать его через `match`, `if let` или `?`;
* объяснить, что `?` делает при `Err`: преобразует ошибку через `From` и завершает функцию;
* помнить, что `?` работает только в функции, чей тип результата совместим (`Result` или `Option`);
* описать `enum` ошибки с вариантами по причинам и реализовать `Display` для понятного сообщения;
* реализовать `From<Чужая> for Своя`, чтобы `?` преобразовывал ошибки автоматически;
* выбирать между `Result` для ожидаемых сбоев и `panic!` или `expect` для нарушенных инвариантов.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Использовать `?` в `main`, который ничего не возвращает: будет ошибка `E0277`.
* Печатать ошибку через `{:?}` пользователю: для людей нужен `Display` (`{}`).
* Писать `unwrap()` на данных, пришедших извне: любая опечатка роняет программу.
* Забыть `impl From`, из-за чего `?` не знает, как преобразовать ошибку.
* Терять причину ошибки, превращая её в строку слишком рано (`map_err(|_| ...)`).
* Ждать, что `collect` в `Result<Vec<_>, _>` продолжит работу после первой ошибки: он остановится на ней.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Обработка ошибок: Result, ? и свои типы ошибок](lesson:rust.fundamentals.error-handling). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
