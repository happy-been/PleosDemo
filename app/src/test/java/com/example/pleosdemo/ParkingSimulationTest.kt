package com.example.pleosdemo

import org.junit.Assert.*
import org.junit.Test

class ParkingSimulationTest {
    private val gas = SimulationInput(throttle = 1f, allowed = true)

    @Test fun blockedInputNeverMovesTheCar() {
        val sim = ParkingSimulation()
        repeat(100) { sim.step(gas.copy(allowed = false), 0.033) }
        assertEquals(ParkingSimulation.INITIAL_POSE, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }

    @Test fun reverseAndForwardHaveOppositeTravelDirections() {
        val reverse = ParkingSimulation()
        val forward = ParkingSimulation().also { it.selectGear(VirtualGear.D) }
        repeat(20) { reverse.step(gas, 0.033); forward.step(gas, 0.033) }
        assertTrue(reverse.pose.rearX < ParkingSimulation.INITIAL_POSE.rearX)
        assertTrue(forward.pose.rearX > ParkingSimulation.INITIAL_POSE.rearX)
    }

    @Test fun brakeImmediatelyStopsAndDoesNotMove() {
        val sim = ParkingSimulation()
        repeat(30) { sim.step(gas, 0.033) }
        val before = sim.pose
        sim.step(gas.copy(brake = true), 0.033)
        assertEquals(before, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }

    @Test fun losingPermissionToDriveStopsImmediately() {
        val sim = ParkingSimulation()
        repeat(30) { sim.step(gas, 0.033) }
        val before = sim.pose
        sim.step(gas.copy(allowed = false), 0.033)
        assertEquals(before, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }

    @Test fun leftSteeringTurnsForwardCarUpwards() {
        val sim = ParkingSimulation().also { it.selectGear(VirtualGear.D) }
        repeat(30) { sim.step(gas.copy(steeringDegrees = -20f), 0.033) }
        assertTrue(sim.pose.heading < 0.0)
        assertTrue(sim.pose.rearY < ParkingSimulation.INITIAL_POSE.rearY)
    }

    @Test fun movingCarCannotChangeDirection() {
        val sim = ParkingSimulation()
        repeat(30) { sim.step(gas, 0.033) }
        assertFalse(sim.selectGear(VirtualGear.D))
        assertEquals(VirtualGear.R, sim.gear)
    }

    @Test fun boundaryCollisionStopsInsideTheWorldAndStaysStopped() {
        val sim = ParkingSimulation()
        repeat(1_000) { sim.step(gas, 0.033) }
        assertTrue(sim.collided)
        assertTrue(sim.corners().all(ParkingSimulation.WORLD::contains))
        val before = sim.pose
        repeat(10) { sim.step(gas, 0.033) }
        assertEquals(before, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }

    @Test fun collisionUsesRotatedBodyAndNotOnlyItsCenter() {
        val sim = ParkingSimulation()
        assertTrue(sim.hasCollision(CarPose(7.5, 4.0, -Math.PI / 4)))
        assertFalse(sim.hasCollision(ParkingSimulation.INITIAL_POSE))
    }

    @Test fun parkingRequiresWholeBodyAndAStoppedHold() {
        val sim = ParkingSimulation(CarPose(9.75, 2.4, 0.0))
        repeat(5) { sim.step(SimulationInput(allowed = true), 0.1) }
        assertFalse(sim.parked)
        repeat(6) { sim.step(SimulationInput(allowed = true), 0.1) }
        assertTrue(sim.parked)
    }

    @Test fun resetClearsCollisionAndVelocity() {
        val sim = ParkingSimulation()
        repeat(1_000) { sim.step(gas, 0.033) }
        sim.reset()
        assertFalse(sim.collided)
        assertEquals(ParkingSimulation.INITIAL_POSE, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }

    @Test fun nonFiniteInputDoesNotCorruptPose() {
        val sim = ParkingSimulation()
        sim.step(gas.copy(steeringDegrees = Float.NaN), 0.033)
        assertEquals(ParkingSimulation.INITIAL_POSE, sim.pose)
        assertEquals(0.0, sim.speedMps, 0.0)
    }
}
