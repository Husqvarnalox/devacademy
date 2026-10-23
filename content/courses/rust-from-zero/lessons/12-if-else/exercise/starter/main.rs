fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let age: i32 = lines.next().unwrap().trim().parse().unwrap();
    let is_student = lines.next().unwrap().trim() == "да";

    // TODO: заполните category и price с помощью if / else if / else.
    let mut category = "взрослый";
    let mut price = 400;
    let _ = (age, is_student, &mut category, &mut price);

    println!("Категория: {category}");
    println!("Цена: {price}");
}
