package com.example.pleosdemo

import kotlin.math.*

data class Point2(val x: Double, val y: Double)
data class ParkingBox(val left: Double, val top: Double, val right: Double, val bottom: Double) {
    fun contains(point: Point2) = point.x in left..right && point.y in top..bottom
    fun corners() = listOf(Point2(left, top), Point2(right, top), Point2(right, bottom), Point2(left, bottom))
}

data class CarPose(val rearX: Double, val rearY: Double, val heading: Double)
enum class VirtualGear(val direction: Int) { D(1), R(-1) }
data class SimulationInput(
    val steeringDegrees: Float = 0f,
    val throttle: Float = 0f,
    val brake: Boolean = false,
    val allowed: Boolean = false,
)

/** 미터/초 단위의 저속 bicycle model. Android나 차량 SDK에 의존하지 않는다. */
class ParkingSimulation(private val initialPose: CarPose = INITIAL_POSE) {
    var pose = initialPose
        private set
    var speedMps = 0.0
        private set
    var gear = VirtualGear.R
        private set
    var collided = false
        private set
    var parked = false
        private set
    private var parkingHoldSeconds = 0.0

    fun reset() {
        pose = initialPose
        speedMps = 0.0
        gear = VirtualGear.R
        collided = false
        parked = false
        parkingHoldSeconds = 0.0
    }

    fun stop() { speedMps = 0.0 }

    fun selectGear(next: VirtualGear): Boolean {
        if (abs(speedMps) > 0.05) return false
        gear = next
        return true
    }

    fun step(input: SimulationInput, elapsedSeconds: Double) {
        if (!input.allowed || collided || parked || !elapsedSeconds.isFinite() ||
            !input.steeringDegrees.isFinite() || !input.throttle.isFinite()) {
            stop()
            parkingHoldSeconds = 0.0
            return
        }
        // 긴 프레임도 작은 시간 간격으로 나눠 충돌을 확인한다.
        var remaining = elapsedSeconds.coerceIn(0.0, 0.1)
        while (remaining > 0.000001) {
            val dt = min(remaining, 0.02)
            integrate(input, dt)
            if (collided || parked) break
            remaining -= dt
        }
    }

    private fun integrate(input: SimulationInput, dt: Double) {
        val throttle = input.throttle.coerceIn(0f, 1f).toDouble()
        val acceleration = if (throttle > 0.0) 1.6 * throttle else -0.45
        val magnitude = if (input.brake) 0.0 else
            (abs(speedMps) + acceleration * dt).coerceIn(0.0, 2.0)
        speedMps = magnitude * gear.direction
        val steering = Math.toRadians(input.steeringDegrees.coerceIn(-35f, 35f).toDouble())
        val yaw = speedMps / WHEELBASE * tan(steering) * dt
        val middleHeading = pose.heading + yaw / 2.0
        val candidate = CarPose(
            pose.rearX + speedMps * cos(middleHeading) * dt,
            pose.rearY + speedMps * sin(middleHeading) * dt,
            atan2(sin(pose.heading + yaw), cos(pose.heading + yaw)),
        )
        if (hasCollision(candidate)) {
            collided = true
            stop()
            return
        }
        pose = candidate
        val parallelError = abs(asin(sin(pose.heading)))
        if (corners(pose).all(TARGET::contains) && parallelError < Math.toRadians(8.0) && magnitude < 0.05) {
            parkingHoldSeconds += dt
            if (parkingHoldSeconds >= 1.0) parked = true
        } else {
            parkingHoldSeconds = 0.0
        }
    }

    fun center(at: CarPose = pose) = Point2(
        at.rearX + CENTER_OFFSET * cos(at.heading),
        at.rearY + CENTER_OFFSET * sin(at.heading),
    )

    fun corners(at: CarPose = pose): List<Point2> {
        val c = center(at)
        return listOf(-1 to -1, 1 to -1, 1 to 1, -1 to 1).map { (sx, sy) ->
            val x = sx * LENGTH / 2.0
            val y = sy * WIDTH / 2.0
            Point2(c.x + x * cos(at.heading) - y * sin(at.heading),
                c.y + x * sin(at.heading) + y * cos(at.heading))
        }
    }

    fun hasCollision(at: CarPose): Boolean {
        val polygon = corners(at)
        return polygon.any { !WORLD.contains(it) } || OBSTACLES.any { overlaps(polygon, it.corners()) }
    }

    companion object {
        const val LENGTH = 4.6
        const val WIDTH = 1.8
        const val WHEELBASE = 2.7
        const val CENTER_OFFSET = WHEELBASE / 2.0
        val WORLD = ParkingBox(0.0, 0.0, 23.0, 13.0)
        val TARGET = ParkingBox(7.6, 1.0, 14.6, 3.8)
        val OBSTACLES = listOf(
            ParkingBox(2.5, 1.5, 7.1, 3.3),
            ParkingBox(15.1, 1.5, 19.7, 3.3),
        )
        val INITIAL_POSE = CarPose(15.5, 6.0, 0.0)

        private fun overlaps(a: List<Point2>, b: List<Point2>): Boolean {
            for (polygon in listOf(a, b)) {
                for (i in polygon.indices) {
                    val p = polygon[i]
                    val q = polygon[(i + 1) % polygon.size]
                    val axisX = -(q.y - p.y)
                    val axisY = q.x - p.x
                    val pa = a.map { it.x * axisX + it.y * axisY }
                    val pb = b.map { it.x * axisX + it.y * axisY }
                    if (pa.max() <= pb.min() || pb.max() <= pa.min()) return false
                }
            }
            return true
        }
    }
}
