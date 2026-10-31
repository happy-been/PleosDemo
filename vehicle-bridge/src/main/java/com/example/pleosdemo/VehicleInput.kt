package com.example.pleosdemo

import kotlin.math.abs

/** SDK 값의 '없음'을 0이나 false로 바꾸지 않고 그대로 전달한다. */
data class VehicleSignal<T>(
    val value: T? = null,
    val note: String = "연결 대기",
    val receivedAtMs: Long = 0L,
) {
    fun isFresh(nowMs: Long): Boolean = value != null &&
        receivedAtMs > 0L && nowMs - receivedAtMs in 0L..3_000L
}

data class VehicleInput(
    val steering: VehicleSignal<Float> = VehicleSignal(),
    val brake: VehicleSignal<Boolean> = VehicleSignal(),
    val gear: VehicleSignal<Int> = VehicleSignal(),
    val speed: VehicleSignal<Float> = VehicleSignal(),
    val rpm: VehicleSignal<Float> = VehicleSignal(),
    val accelerator: VehicleSignal<Float> = VehicleSignal(),
    val brakePercent: VehicleSignal<Float> = VehicleSignal(),
) {
    /** 실제 기어의 P 값은 AAOS VehicleGear.GEAR_PARK(4)이다. */
    fun blockedReason(nowMs: Long): String? = when {
        !gear.isFresh(nowMs) -> "AVD 기어 정보를 기다리는 중"
        gear.value != 4 -> "AVD를 P단으로 변경하세요"
        !speed.isFresh(nowMs) -> "AVD 속도 정보를 기다리는 중"
        speed.value?.isFinite() != true -> "AVD 속도 값을 확인하세요"
        abs(speed.value ?: 0f) > 0.01f -> "AVD 속도가 0이어야 시작할 수 있어요"
        !steering.isFresh(nowMs) -> "조향 입력을 확인하세요"
        steering.value?.isFinite() != true -> "조향 값을 확인하세요"
        !brake.isFresh(nowMs) -> "브레이크 입력을 확인하세요"
        else -> null
    }
}
