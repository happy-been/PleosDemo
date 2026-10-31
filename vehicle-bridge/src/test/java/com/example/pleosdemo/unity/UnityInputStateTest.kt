package com.example.pleosdemo.unity

import com.example.pleosdemo.AvdPedals
import com.example.pleosdemo.VehicleInput
import com.example.pleosdemo.VehicleSignal
import org.junit.Assert.*
import org.junit.Test

class UnityInputStateTest {
    private val now = 10_000L
    private fun vehicle() = VehicleInput(
        steering = VehicleSignal(-17.5f, receivedAtMs = now),
        brake = VehicleSignal(false, receivedAtMs = now),
        gear = VehicleSignal(4, receivedAtMs = now),
        speed = VehicleSignal(0f, receivedAtMs = now),
    )
    private fun pedals() = AvdPedals(37f, 50f, now)

    @Test fun convertsSdkSteeringSignForUnityAndNormalizesPedals() {
        val state = UnityInputState.from(vehicle(), pedals(), now, true)
        assertTrue(state.allowed)
        assertEquals(17.5f, state.steeringDegrees, 0f)
        assertEquals(0.5f, state.steeringNormalized, 0f)
        assertEquals(0.37f, state.throttle, 0.0001f)
        assertEquals(0.5f, state.brake, 0f)
        val opposite = UnityInputState.from(vehicle().copy(steering = VehicleSignal(17.5f, receivedAtMs = now)), pedals(), now, true)
        assertEquals(-17.5f, opposite.steeringDegrees, 0f)
        assertEquals(-0.5f, opposite.steeringNormalized, 0f)
    }

    @Test fun nonParkGearRemovesThrottleAndAppliesBrake() {
        val state = UnityInputState.from(vehicle().copy(gear = VehicleSignal(8, receivedAtMs = now)), pedals(), now, true)
        assertFalse(state.allowed)
        assertEquals(0f, state.throttle, 0f)
        assertEquals(1f, state.brake, 0f)
    }

    @Test fun oldPedalSampleIsNotRefreshedByReadingItAgain() {
        val state = UnityInputState.from(vehicle(), pedals().copy(receivedAtMs = now - 601L), now, true)
        assertFalse(state.allowed)
        assertEquals(601L, state.pedalAgeMs)
        assertEquals(0f, state.throttle, 0f)
    }

    @Test fun staleSteeringStopsControl() {
        val state = UnityInputState.from(vehicle().copy(steering = VehicleSignal(12f, receivedAtMs = now - 3_001L)), pedals(), now, true)
        assertFalse(state.allowed)
        assertEquals(1f, state.brake, 0f)
    }

    @Test fun futurePedalTimestampIsInvalid() {
        assertFalse(UnityInputState.from(vehicle(), pedals().copy(receivedAtMs = now + 1L), now, true).allowed)
    }

    @Test fun invalidPedalPercentageCannotDriveTheCar() {
        assertFalse(UnityInputState.from(vehicle(), pedals().copy(acceleratorPercent = 101f), now, true).allowed)
    }

    @Test fun missingVehicleSignalsAreNotReplacedWithValidZeroes() {
        assertFalse(UnityInputState.from(VehicleInput(), pedals(), now, true).allowed)
    }

    @Test fun stoppingBridgeStopsControlEvenWithFreshCachedSamples() {
        val state = UnityInputState.from(vehicle(), pedals(), now, false)
        assertFalse(state.allowed)
        assertEquals(0f, state.throttle, 0f)
        assertEquals(1f, state.brake, 0f)
    }
}
