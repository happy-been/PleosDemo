package com.example.pleosdemo

import org.junit.Assert.assertTrue
import org.junit.Test

/** The 2D simulator owns this behavior; the shared input library has no simulator dependency. */
class PedalSimulationTest {
    @Test fun strongerPedalMovesTheSimulatorFaster() {
        val light = ParkingSimulation()
        val heavy = ParkingSimulation()
        repeat(20) {
            light.step(SimulationInput(throttle = 0.25f, allowed = true), 0.033)
            heavy.step(SimulationInput(throttle = 0.75f, allowed = true), 0.033)
        }
        assertTrue(kotlin.math.abs(light.speedMps) > 0.0)
        assertTrue(kotlin.math.abs(heavy.speedMps) > kotlin.math.abs(light.speedMps))
    }
}
