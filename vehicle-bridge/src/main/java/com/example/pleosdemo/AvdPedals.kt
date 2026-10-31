package com.example.pleosdemo

/** PC 콘솔에서 AVD에 전달한 페달 입력. 비율은 0~100이다. */
data class AvdPedals(
    val acceleratorPercent: Float? = null,
    val brakePercent: Float? = null,
    val receivedAtMs: Long = 0L,
    val note: String = "PC 콘솔 연결 대기",
) {
    fun blockedReason(nowMs: Long): String? = when {
        acceleratorPercent == null || brakePercent == null -> note
        !acceleratorPercent.isFinite() || acceleratorPercent !in 0f..100f -> "가속페달 값을 확인하세요"
        !brakePercent.isFinite() || brakePercent !in 0f..100f -> "브레이크 값을 확인하세요"
        receivedAtMs <= 0L || nowMs - receivedAtMs !in 0L..600L -> "AVD 페달 입력 갱신이 끊겼어요"
        else -> null
    }

    fun throttle(nowMs: Long): Float = if (blockedReason(nowMs) == null)
        (acceleratorPercent ?: 0f) / 100f else 0f
}
