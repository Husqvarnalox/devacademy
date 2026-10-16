use std::collections::HashMap;
use std::io::{self, Read};

/// Разбивает текст на слова: последовательности букв и цифр, в нижнем регистре.
fn tokenize(text: &str) -> Vec<String> {
    todo!("split по не-буквенно-цифровым символам, filter пустых, map to_lowercase")
}

/// Считает частоты слов.
fn count(words: &[String]) -> HashMap<&str, usize> {
    todo!("entry(..).or_insert(0)")
}

/// Возвращает не более n пар (слово, число): по убыванию числа, затем по алфавиту.
fn top<'a>(freq: &HashMap<&'a str, usize>, n: usize) -> Vec<(&'a str, usize)> {
    todo!("собрать в Vec, sort_by, take(n)")
}

fn main() {
    let mut input = String::new();
    io::stdin().read_to_string(&mut input).unwrap();
    let (first, text) = input.split_once('\n').unwrap_or((input.as_str(), ""));
    let n: usize = first.trim().parse().unwrap_or(0);

    let words = tokenize(text);
    let freq = count(&words);
    for (w, c) in top(&freq, n) {
        println!("{w} {c}");
    }
    println!("unique={} total={}", freq.len(), words.len());
}
