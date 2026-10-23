fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let score: i32 = lines.next().unwrap().trim().parse().unwrap();
    let day = lines.next().unwrap().trim();

    let grade = match score {
        90..=100 => "отлично",
        75..=89 => "хорошо",
        60..=74 => "удовлетворительно",
        0..=59 => "неудовлетворительно",
        _ => "ошибка: балл вне диапазона 0-100",
    };
    let day_type = match day {
        "сб" | "вс" => "выходной",
        "пн" | "вт" | "ср" | "чт" | "пт" => "рабочий",
        _ => "неизвестный день",
    };

    println!("Оценка: {grade}");
    println!("День: {day_type}");
}
