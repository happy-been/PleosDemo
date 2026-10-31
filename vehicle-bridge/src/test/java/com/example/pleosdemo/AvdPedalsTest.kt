package com.example.pleosdemo

import org.junit.Assert.*
import org.junit.Test

class AvdPedalsTest {
    private val now = 10_000L
    private val connected = AvdPedals(37f, 0f, now)

    @Test fun pedalPercentageBecomesProportionalThrottle() {
        assertNull(connected.blockedReason(now))
        assertEquals(0.37f, connected.throttle(now), 0.0001f)
    }
    @Test fun releasedPedalHasZeroThrottle() {
        assertEquals(0f, connected.copy(acceleratorPercent = 0f).throttle(now), 0f)
    }
    @Test fun missingConnectionBlocksAndDoesNotAccelerate() {
        assertNotNull(AvdPedals().blockedReason(now))
        assertEquals(0f, AvdPedals().throttle(now), 0f)
    }
    @Test fun oldOrFutureSamplesDoNotKeepThePedalPressed() {
        assertEquals(0f, connected.throttle(now + 601), 0f)
        assertEquals(0f, connected.throttle(now - 1), 0f)
    }
    @Test fun nonfiniteAndOutOfRangeValuesAreRejected() {
        for (value in listOf(Float.NaN, Float.POSITIVE_INFINITY, -1f, 101f)) {
            assertNotNull(connected.copy(acceleratorPercent = value).blockedReason(now))
            assertEquals(0f, connected.copy(acceleratorPercent = value).throttle(now), 0f)
        }
    }
    @Test fun invalidBrakeSampleBlocksTheInputStream() {
        assertNotNull(connected.copy(brakePercent = null).blockedReason(now))
        assertNotNull(connected.copy(brakePercent = 110f).blockedReason(now))
    }
}
