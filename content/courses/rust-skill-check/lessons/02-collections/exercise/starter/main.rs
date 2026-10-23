use std::collections::HashMap;

// Делит строку "имя;число" по первой ';'. None: нет ';', пустое имя или число не разобралось.
fn split_pair(line: &str) -> Option<(String, i32)> {
    // TODO: split_once(';')?, trim, проверка имени, parse::<i32>().ok()?
    Some((String::from(line), 0))
}

// Суммирует amounts по категориям: categories[i] соответствует amounts[i].
fn totals_by_category(categories: &Vec<String>, amounts: &Vec<i32>) -> HashMap<String, i32> {
    // TODO: *totals.entry(ключ).or_insert(0) += сумма
    HashMap::new()
}

// Категория с наибольшей суммой; при равенстве - первая по алфавиту; для пустого словаря - "-".
fn biggest(totals: &HashMap<String, i32>) -> String {
    // TODO: обходите ключи по алфавиту и обновляйте ответ только при строго большей сумме
    String::from("?")
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut categories: Vec<String> = Vec::new();
    let mut amounts: Vec<i32> = Vec::new();
    let mut skipped = 0;
    for line in input.lines() {
        match split_pair(line) {
            Some((name, value)) => {
                categories.push(name);
                amounts.push(value);
            }
            None => skipped += 1,
        }
    }
    let totals = totals_by_category(&categories, &amounts);
    let mut names: Vec<&String> = totals.keys().collect();
    names.sort();
    for name in names {
        println!("{}: {}", name, totals[name]);
    }
    println!("top: {}", biggest(&totals));
    println!("skipped: {skipped}");
}
