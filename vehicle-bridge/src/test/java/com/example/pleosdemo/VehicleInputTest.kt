package com.example.pleosdemo

import org.junit.Assert.*
import org.junit.Test

class VehicleInputTest {
    private val now = 10_000L
    private val valid = VehicleInput(
        steering = VehicleSignal(0f, receivedAtMs = now),
        brake = VehicleSignal(false, receivedAtMs = now),
        gear = VehicleSignal(4, receivedAtMs = now),
        speed = VehicleSignal(0f, receivedAtMs = now),
    )

    @Test fun parkedVehicleWithFreshCoreInputsIsAllowed() { assertNull(valid.blockedReason(now)) }
    @Test fun missingValuesAreNotTreatedAsParked() { assertNotNull(VehicleInput().blockedReason(now)) }
    @Test fun leavingParkBlocksImmediately() {
        assertNotNull(valid.copy(gear = VehicleSignal(8, receivedAtMs = now)).blockedReason(now))
    }
    @Test fun nonzeroActualSpeedBlocksEvenInPark() {
        assertNotNull(valid.copy(speed = VehicleSignal(-0.5f, receivedAtMs = now)).blockedReason(now))
    }
    @Test fun staleAndFutureSignalsAreRejected() {
        assertNotNull(valid.blockedReason(now + 3_001L))
        assertNotNull(valid.blockedReason(now - 1L))
    }
    @Test fun missingBrakeIsNotTreatedAsReleased() {
        assertNotNull(valid.copy(brake = VehicleSignal()).blockedReason(now))
    }
    @Test fun nonfiniteSpeedIsRejected() {
        assertNotNull(valid.copy(speed = VehicleSignal(Float.NaN, receivedAtMs = now)).blockedReason(now))
    }
}
