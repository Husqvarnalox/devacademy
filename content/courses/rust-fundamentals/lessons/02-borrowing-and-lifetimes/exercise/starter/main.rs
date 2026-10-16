use std::io::Read;

// TODO: структура хранит ссылки на части входного текста - ей нужен параметр времени жизни
struct Words {
    items: Vec<&str>,
}

// TODO: два входа и ссылка в результате: компилятор не знает, от какого из входов зависит результат
fn pick(a: &str, b: &str) -> &str {
    if b.chars().count() > a.chars().count() { b } else { a }
}

impl Words {
    fn new(text: &str) -> Self {
        Words { items: text.split_whitespace().collect() }
    }

    fn first(&self) -> &str {
        self.items[0]
    }

    fn last(&self) -> &str {
        self.items[self.items.len() - 1]
    }

    fn longest(&self) -> &str {
        let mut best = self.items[0];
        for w in &self.items {
            best = pick(best, w);
        }
        best
    }
}

fn main() {
    let mut text = String::new();
    std::io::stdin().read_to_string(&mut text).unwrap();
    let words = Words::new(&text);

    println!("count={}", words.items.len());
    println!("first={}", words.first());
    println!("last={}", words.last());
    println!("longest={}", words.longest());
}
