fn count_above(values: &Vec<i32>, limit: i32) -> i32 {
    // TODO: посчитайте измерения строго больше limit
    0
}

fn largest(values: &Vec<i32>) -> Option<i32> {
    // TODO: найдите наибольшее измерение или верните None для пустого вектора
    None
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = input.lines();
    let limit = lines.next().unwrap_or("0").trim().parse::<i32>().unwrap_or(0);
    let mut temps: Vec<i32> = Vec::new();
    for line in lines {
        if let Ok(t) = line.trim().parse::<i32>() {
            temps.push(t);
        }
    }
    println!("Измерений: {}", temps.len());
    println!("Выше {limit}: {}", count_above(&temps, limit));
    match largest(&temps) {
        Some(m) => println!("Максимум: {m}"),
        None => println!("Максимум: нет данных"),
    }
}
