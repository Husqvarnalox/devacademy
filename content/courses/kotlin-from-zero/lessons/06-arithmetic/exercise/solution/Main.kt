fun main() {
    val apples = 47
    val perBox = 6
    val boxPrice = 350
    val fullBoxes = apples / perBox
    val rest = apples % perBox
    val cost = fullBoxes * boxPrice
    println("Полных коробок: $fullBoxes")
    println("Осталось яблок: $rest")
    println("Стоимость коробок: $cost руб.")
}
