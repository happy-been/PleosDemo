package com.example.pleosdemo

import android.car.Car
import android.car.VehiclePropertyIds
import android.car.hardware.CarPropertyValue
import android.car.hardware.property.CarPropertyManager
import android.content.Context
import android.content.pm.PackageManager
import android.os.Handler
import android.os.HandlerThread
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import ai.pleos.playground.vehicle.Vehicle
import ai.pleos.playground.vehicle.listener.BrakePedalStatusListener
import ai.pleos.playground.vehicle.listener.SteeringWheelAngleListener

/** 조회 전용 어댑터. 차량을 제어하는 set API는 사용하지 않는다. */
@Suppress("DEPRECATION") // AVD의 Android 14에서도 동작하는 구독 API를 사용한다.
class PleosVehicleInput(
    private val context: Context,
    private val onUpdate: (VehicleInput) -> Unit,
) {
    private val main = Handler(Looper.getMainLooper())
    @Volatile private var generation = 0
    @Volatile private var active = false
    private var snapshot = VehicleInput()
    private var vehicle: Vehicle? = null
    private var steeringListener: SteeringWheelAngleListener? = null
    private var brakeListener: BrakePedalStatusListener? = null
    private var car: Car? = null
    private var propertyManager: CarPropertyManager? = null
    private var workerThread: HandlerThread? = null
    private var worker: Handler? = null
    private var propertyCallback: CarPropertyManager.CarPropertyEventCallback? = null
    private val accessibleProperties = mutableSetOf<Int>()

    fun start() {
        if (active) return
        active = true
        val run = ++generation
        snapshot = VehicleInput()
        onUpdate(snapshot)
        startPleos(run)
        if (context.packageManager.hasSystemFeature(PackageManager.FEATURE_AUTOMOTIVE)) {
            startCarProperties(run)
        } else {
            failProperties(run, "자동차 환경에서 사용 가능")
        }
    }

    private fun update(run: Int, transform: (VehicleInput) -> VehicleInput) {
        main.post {
            if (active && generation == run) {
                snapshot = transform(snapshot)
                onUpdate(snapshot)
            }
        }
    }

    private fun steering(run: Int, value: Float?, note: String = "Pleos SDK") {
        val valid = value?.takeIf { it.isFinite() }
        update(run) { it.copy(steering = VehicleSignal(valid,
            if (valid != null) note else if (value != null) "잘못된 값" else note,
            SystemClock.elapsedRealtime())) }
    }

    private fun brake(run: Int, value: Boolean?, note: String = "Pleos SDK") {
        update(run) { it.copy(brake = VehicleSignal(value, note, SystemClock.elapsedRealtime())) }
    }

    private fun startPleos(run: Int) {
        try {
            val sdk = Vehicle(context.applicationContext)
            vehicle = sdk
            sdk.initialize()
            val steeringApi = sdk.getSteeringWheel()
            val brakeApi = sdk.getBrake()
            steeringListener = object : SteeringWheelAngleListener {
                override fun onSteeringWheelAngleUpdated(angle: Float?) = steering(run, angle)
                override fun onFailed(e: Exception) = steering(run, null, errorText(e))
            }.also { steeringApi.registerSteeringWheelAngle(it) }
            brakeListener = object : BrakePedalStatusListener {
                override fun onBrakePedalStatusUpdated(status: Boolean?) = brake(run, status)
                override fun onFailed(e: Exception) = brake(run, null, errorText(e))
            }.also { brakeApi.registerBrakePedalStatus(it) }
            steeringApi.checkSteeringWheelAngleCapabilities(
                onComplete = { config, bounds -> Log.d(TAG, "Steering capability: $config $bounds") },
                onFailed = { Log.w(TAG, "Steering capability: ${errorText(it)}") },
            )
            brakeApi.checkBrakePedalStatusCapabilities(
                onComplete = { Log.d(TAG, "Brake capability: $it") },
                onFailed = { Log.w(TAG, "Brake capability: ${errorText(it)}") },
            )
            // 구독은 즉시 반영하고, 주기적인 get으로 연결 상태와 값의 유효성을 확인한다.
            main.post(object : Runnable {
                override fun run() {
                    if (!active || generation != run) return
                    try {
                        steeringApi.getCurrentSteeringWheelAngle(
                            onComplete = { steering(run, it, if (it == null) "값 없음" else "Pleos SDK") },
                            onFailed = { steering(run, null, errorText(it)) },
                        )
                        brakeApi.getCurrentBrakePedalStatus(
                            onComplete = { brake(run, it, if (it == null) "값 없음" else "Pleos SDK") },
                            onFailed = { brake(run, null, errorText(it)) },
                        )
                    } catch (e: Exception) {
                        steering(run, null, errorText(e))
                        brake(run, null, errorText(e))
                    }
                    main.postDelayed(this, 1_000L)
                }
            })
        } catch (e: Exception) {
            steering(run, null, errorText(e))
            brake(run, null, errorText(e))
        }
    }

    private fun startCarProperties(run: Int) {
        val thread = HandlerThread("parking-vehicle-data").also { it.start() }
        workerThread = thread
        val handler = Handler(thread.looper)
        worker = handler
        try {
            car = Car.createCar(context.applicationContext, handler, Car.CAR_WAIT_TIMEOUT_DO_NOT_WAIT) { connectedCar, ready ->
                handler.post {
                    if (!active || generation != run) return@post
                    if (!ready) {
                        propertyManager = null
                        accessibleProperties.clear()
                        failProperties(run, "차량 서비스 연결 끊김")
                    } else {
                        try {
                            val manager = connectedCar.getCarManager(Car.PROPERTY_SERVICE) as CarPropertyManager
                            propertyManager = manager
                            subscribeProperties(manager, run)
                        } catch (e: Exception) {
                            failProperties(run, errorText(e))
                        }
                    }
                }
            }
        } catch (e: Exception) {
            failProperties(run, errorText(e))
        }
    }

    private fun subscribeProperties(manager: CarPropertyManager, run: Int) {
        accessibleProperties.clear()
        propertyCallback?.let { runCatching { manager.unregisterCallback(it) } }
        val callback = object : CarPropertyManager.CarPropertyEventCallback {
            override fun onChangeEvent(value: CarPropertyValue<*>) {
                if (value.status == CarPropertyValue.STATUS_AVAILABLE) {
                    property(run, value.propertyId, value.value)
                } else property(run, value.propertyId, null, "일시적으로 읽을 수 없음")
            }
            override fun onErrorEvent(propertyId: Int, areaId: Int) {
                property(run, propertyId, null, "차량 데이터 오류")
            }
        }
        propertyCallback = callback
        for (id in PROPERTY_IDS) {
            try {
                if (manager.getCarPropertyConfig(id) == null) {
                    property(run, id, null, "미지원 또는 접근 불가")
                } else {
                    accessibleProperties.add(id)
                    manager.registerCallback(callback, id, CarPropertyManager.SENSOR_RATE_UI)
                }
            } catch (e: Exception) {
                property(run, id, null, errorText(e))
            }
        }
        // ON_CHANGE 값도 다시 읽어 서비스 단절이나 상태 변경을 놓치지 않는다.
        worker?.removeCallbacksAndMessages(POLL_TOKEN)
        val handler = worker ?: return
        val poll = object : Runnable {
            override fun run() {
                if (!active || generation != run || propertyManager !== manager) return
                for (id in accessibleProperties.toList()) {
                    try {
                        val value = if (id == VehiclePropertyIds.GEAR_SELECTION)
                            manager.getProperty(Int::class.javaObjectType, id, 0)
                        else manager.getProperty(Float::class.javaObjectType, id, 0)
                        if (value.status == CarPropertyValue.STATUS_AVAILABLE)
                            property(run, id, value.value)
                        else property(run, id, null, "일시적으로 읽을 수 없음")
                    } catch (e: Exception) {
                        property(run, id, null, errorText(e))
                    }
                }
                handler.postAtTime(this, POLL_TOKEN, SystemClock.uptimeMillis() + 1_000L)
            }
        }
        handler.post(poll)
    }

    private fun property(run: Int, id: Int, value: Any?, note: String = "AAOS · 조회") {
        val now = SystemClock.elapsedRealtime()
        val number = (value as? Number)?.toFloat()?.takeIf { it.isFinite() }
        val signal = VehicleSignal(number, if (number == null && value != null) "잘못된 값" else note, now)
        update(run) {
            when (id) {
                VehiclePropertyIds.GEAR_SELECTION -> it.copy(gear = VehicleSignal(value as? Int, note, now))
                VehiclePropertyIds.PERF_VEHICLE_SPEED -> it.copy(speed = signal)
                VehiclePropertyIds.ENGINE_RPM -> it.copy(rpm = signal)
                VehiclePropertyIds.ACCELERATOR_PEDAL_COMPRESSION_PERCENTAGE -> it.copy(accelerator = signal)
                VehiclePropertyIds.BRAKE_PEDAL_COMPRESSION_PERCENTAGE -> it.copy(brakePercent = signal)
                else -> it
            }
        }
    }

    private fun failProperties(run: Int, note: String) {
        PROPERTY_IDS.forEach { property(run, it, null, note) }
    }

    fun stop() {
        active = false
        generation++
        main.removeCallbacksAndMessages(null)
        worker?.removeCallbacksAndMessages(null)
        val sdk = vehicle
        runCatching { steeringListener?.let { sdk?.getSteeringWheel()?.unregisterSteeringWheelAngle(it) } }
        runCatching { brakeListener?.let { sdk?.getBrake()?.unregisterBrakePedalStatus(it) } }
        runCatching { sdk?.release() }
        vehicle = null
        steeringListener = null
        brakeListener = null
        val manager = propertyManager
        val callback = propertyCallback
        val connection = car
        val thread = workerThread
        val handler = worker
        propertyManager = null
        propertyCallback = null
        car = null
        worker = null
        workerThread = null
        if (handler != null) handler.post {
            runCatching { if (manager != null && callback != null) manager.unregisterCallback(callback) }
            runCatching { connection?.disconnect() }
            thread?.quitSafely()
        }
        else runCatching { connection?.disconnect() }
    }

    companion object {
        private const val TAG = "ParkingVehicle"
        private val POLL_TOKEN = Any()
        private val PROPERTY_IDS = listOf(
            VehiclePropertyIds.GEAR_SELECTION,
            VehiclePropertyIds.PERF_VEHICLE_SPEED,
            VehiclePropertyIds.ENGINE_RPM,
            VehiclePropertyIds.ACCELERATOR_PEDAL_COMPRESSION_PERCENTAGE,
            VehiclePropertyIds.BRAKE_PEDAL_COMPRESSION_PERCENTAGE,
        )
        private fun errorText(e: Exception) = when (e) {
            is SecurityException -> "권한이 허용되지 않음"
            else -> (e.message ?: e.javaClass.simpleName).replace('\n', ' ').take(70)
        }
    }
}
