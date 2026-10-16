fun main() {
    val n = readln().trim().toInt()
    val nums = readln().trim().split(" ").filter { it.isNotEmpty() }.map { it.toInt() }
    val m = readln().trim().toInt()
    val t = readln().trim().toInt()
    check(nums.size == n)
    var inspected = 0
    val result = nums.asSequence()
        .onEach { inspected++ }
        .filter { it % 2 != 0 && it > t }
        .map { it * it }
        .take(m)
        .toList()
    println(if (result.isEmpty()) "(none)" else result.joinToString(" "))
    println("inspected=$inspected")
}
