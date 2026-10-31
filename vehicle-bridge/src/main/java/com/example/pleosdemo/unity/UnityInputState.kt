package com.example.pleosdemo.unity

import com.example.pleosdemo.AvdPedals
import com.example.pleosdemo.VehicleInput

/** Platform-independent values crossing the Kotlin -> Unity boundary. */
data class UnityInputState(
    val active: Boolean,
    val allowed: Boolean,
    val blockReason: String,
    val steeringDegrees: Float,
    val steeringNormalized: Float,
    val acceleratorPercent: Float,
    val brakePercent: Float,
    val throttle: Float,
    val brake: Float,
    val sdkBrakePressed: Boolean,
    val realGear: Int,
    val realSpeedMps: Float,
    val steeringAgeMs: Long,
    val pedalAgeMs: Long,
) {
    companion object {
        fun from(vehicle: VehicleInput, pedals: AvdPedals, nowMs: Long, active: Boolean): UnityInputState {
            val reason = if (!active) "Bridge is not active"
                else vehicle.blockedReason(nowMs) ?: pedals.blockedReason(nowMs)
            val allowed = reason == null
            // Pleos steering uses the opposite sign to Unity's positive-right yaw.
            val angle = -(vehicle.steering.value?.takeIf { it.isFinite() } ?: 0f)
            val accelerator = pedals.acceleratorPercent?.takeIf { it.isFinite() }?.coerceIn(0f, 100f) ?: 0f
            val brakePercent = pedals.brakePercent?.takeIf { it.isFinite() }?.coerceIn(0f, 100f) ?: 0f
            return UnityInputState(
                active = active,
                allowed = allowed,
                blockReason = reason.orEmpty(),
                steeringDegrees = angle,
                steeringNormalized = (angle / 35f).coerceIn(-1f, 1f),
                acceleratorPercent = accelerator,
                brakePercent = brakePercent,
                throttle = if (allowed) accelerator / 100f else 0f,
                brake = if (allowed) brakePercent / 100f else 1f,
                sdkBrakePressed = vehicle.brake.value ?: false,
                realGear = vehicle.gear.value ?: -1,
                realSpeedMps = vehicle.speed.value?.takeIf { it.isFinite() } ?: 0f,
                steeringAgeMs = age(nowMs, vehicle.steering.receivedAtMs),
                pedalAgeMs = age(nowMs, pedals.receivedAtMs),
            )
        }

        private fun age(nowMs: Long, receivedAtMs: Long) =
            if (receivedAtMs <= 0L) -1L else nowMs - receivedAtMs
    }
}
