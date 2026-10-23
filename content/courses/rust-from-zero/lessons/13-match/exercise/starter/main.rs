fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let score: i32 = lines.next().unwrap().trim().parse().unwrap();
    let day = lines.next().unwrap().trim();

    // TODO: замените пустые строки результатом match.
    let grade = "";
    let day_type = "";
    let _ = (score, day);

    println!("Оценка: {grade}");
    println!("День: {day_type}");
}
