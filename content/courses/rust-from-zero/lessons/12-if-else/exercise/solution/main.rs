fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let age: i32 = lines.next().unwrap().trim().parse().unwrap();
    let is_student = lines.next().unwrap().trim() == "да";

    let mut category = "взрослый";
    let mut price = 400;
    if age < 6 {
        category = "бесплатный";
        price = 0;
    } else if age <= 17 {
        category = "детский";
        price = 200;
    } else if age >= 65 {
        category = "пенсионный";
        price = 250;
    } else if is_student {
        category = "студенческий";
        price = 300;
    }

    println!("Категория: {category}");
    println!("Цена: {price}");
}
