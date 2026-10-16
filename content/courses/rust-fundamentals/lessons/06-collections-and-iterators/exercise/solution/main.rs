use std::collections::HashMap;
use std::io::{self, Read};

/// Разбивает текст на слова: последовательности букв и цифр, в нижнем регистре.
fn tokenize(text: &str) -> Vec<String> {
    text.split(|c: char| !c.is_alphanumeric())
        .filter(|w| !w.is_empty())
        .map(|w| w.to_lowercase())
        .collect()
}

/// Считает частоты слов.
fn count(words: &[String]) -> HashMap<&str, usize> {
    let mut m = HashMap::new();
    for w in words {
        *m.entry(w.as_str()).or_insert(0) += 1;
    }
    m
}

/// Возвращает не более n пар (слово, число): по убыванию числа, затем по алфавиту.
fn top<'a>(freq: &HashMap<&'a str, usize>, n: usize) -> Vec<(&'a str, usize)> {
    let mut pairs: Vec<(&str, usize)> = freq.iter().map(|(w, c)| (*w, *c)).collect();
    pairs.sort_by(|a, b| b.1.cmp(&a.1).then(a.0.cmp(b.0)));
    pairs.into_iter().take(n).collect()
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
