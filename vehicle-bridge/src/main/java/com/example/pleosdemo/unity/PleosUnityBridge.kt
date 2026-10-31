package com.example.pleosdemo.unity

import android.app.Activity
import android.content.pm.PackageManager
import android.content.pm.PermissionInfo
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import com.example.pleosdemo.AvdConsolePedals
import com.example.pleosdemo.AvdPedals
import com.example.pleosdemo.PleosVehicleInput
import com.example.pleosdemo.VehicleInput
import org.json.JSONObject
import java.util.concurrent.atomic.AtomicLong

/** JNI facade. SDK lifecycle runs on Android's main thread; reads return immutable snapshots. */
object PleosUnityBridge {
    private val main = Handler(Looper.getMainLooper())
    private val sequence = AtomicLong()
    @Volatile private var active = false
    @Volatile private var vehicle = VehicleInput()
    @Volatile private var pedals = AvdPedals()
    private var vehicleSource: PleosVehicleInput? = null
    private var pedalSource: AvdConsolePedals? = null

    @JvmStatic
    fun start(activity: Activity) {
        main.post {
            stopInternal()
            vehicle = VehicleInput()
            pedals = AvdPedals()
            active = true
            vehicleSource = PleosVehicleInput(activity) { value ->
                vehicle = value
                sequence.incrementAndGet()
            }.also { it.start() }
            pedalSource = AvdConsolePedals { value ->
                pedals = value
                sequence.incrementAndGet()
            }.also { it.start() }
            requestPermissions(activity)
            Log.i("PleosUnityBridge", "Started: steering/brake SDK + AVD pedal adapter")
        }
    }

    @JvmStatic
    fun stop() {
        main.post { stopInternal() }
    }

    private fun stopInternal() {
        active = false
        vehicleSource?.stop()
        pedalSource?.stop()
        vehicleSource = null
        pedalSource = null
        sequence.incrementAndGet()
    }

    @JvmStatic
    fun readSnapshot(): String {
        val state = UnityInputState.from(vehicle, pedals, SystemClock.elapsedRealtime(), active)
        return JSONObject().apply {
            put("schemaVersion", 1)
            put("sequence", sequence.get())
            put("source", "Pleos SDK + AVD console pedals")
            put("active", state.active)
            put("allowed", state.allowed)
            put("blockReason", state.blockReason)
            put("steeringDegrees", state.steeringDegrees)
            put("steeringNormalized", state.steeringNormalized)
            put("acceleratorPercent", state.acceleratorPercent)
            put("brakePercent", state.brakePercent)
            put("throttle", state.throttle)
            put("brake", state.brake)
            put("sdkBrakePressed", state.sdkBrakePressed)
            put("realGear", state.realGear)
            put("realSpeedMps", state.realSpeedMps)
            put("steeringAgeMs", state.steeringAgeMs)
            put("pedalAgeMs", state.pedalAgeMs)
        }.toString()
    }

    private fun requestPermissions(activity: Activity) {
        val permissions = listOf(
            "android.car.permission.CAR_SPEED",
            "android.car.permission.READ_CAR_PEDALS",
            "android.car.permission.CAR_ENGINE_DETAILED_3P",
        ).filter { name ->
            val info = runCatching { activity.packageManager.getPermissionInfo(name, 0) }.getOrNull()
            info != null && info.protection == PermissionInfo.PROTECTION_DANGEROUS &&
                activity.checkSelfPermission(name) != PackageManager.PERMISSION_GRANTED
        }
        if (permissions.isNotEmpty()) activity.requestPermissions(permissions.toTypedArray(), 420)
    }
}
