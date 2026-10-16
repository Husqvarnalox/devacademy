// Нужно: квадраты первых m нечётных чисел больше t и сколько элементов источника было просмотрено.
fun main() {
    val n = readln().trim().toInt()
    val nums = readln().trim().split(" ").filter { it.isNotEmpty() }.map { it.toInt() }
    val m = readln().trim().toInt()
    val t = readln().trim().toInt()
    check(nums.size == n)
    var inspected = 0
    // TODO: сейчас конвейер жадный: каждый шаг обходит весь список.
    // Сделайте его ленивым (asSequence), чтобы inspected отражал только реально нужные элементы.
    val result = nums
        .onEach { inspected++ }
        .filter { it % 2 != 0 && it > t }
        .map { it * it }
        .take(m)
    println(if (result.isEmpty()) "(none)" else result.joinToString(" "))
    println("inspected=$inspected")
}
