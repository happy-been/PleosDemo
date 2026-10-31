package com.example.pleosdemo

import android.content.pm.PackageManager
import android.content.pm.PermissionInfo
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.view.Gravity
import android.view.MotionEvent
import android.view.View
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import java.util.Locale
import kotlin.math.abs

class MainActivity : AppCompatActivity() {
    private val simulation = ParkingSimulation()
    private val handler = Handler(Looper.getMainLooper())
    private lateinit var source: PleosVehicleInput
    private lateinit var pedalSource: AvdConsolePedals
    private var vehicleInput = VehicleInput()
    private var avdPedals = AvdPedals()
    private lateinit var parkingView: ParkingView
    private lateinit var liveModeButton: Button
    private lateinit var demoModeButton: Button
    private lateinit var startButton: Button
    private lateinit var driveButton: Button
    private lateinit var reverseButton: Button
    private lateinit var gasButton: Button
    private lateinit var brakeButton: Button
    private lateinit var steeringBar: SeekBar
    private lateinit var steeringLabel: TextView
    private lateinit var statusText: TextView
    private lateinit var simulationText: TextView
    private lateinit var accelerationText: TextView
    private val signalViews = linkedMapOf<String, TextView>()
    private var useVehicleInput = true
    private var running = false
    private var gasHeld = false
    private var brakeHeld = false
    private var screenSteering = 0f
    private var lastFrameMs = 0L
    private var lastUiMs = 0L
    private var foreground = false
    private val ink = Color.rgb(9, 17, 29)
    private val panel = Color.rgb(20, 32, 48)
    private val muted = Color.rgb(158, 177, 198)
    private val mint = Color.rgb(86, 231, 188)
    private val white = Color.rgb(236, 243, 249)

    private val frame = object : Runnable {
        override fun run() {
            if (!foreground) return
            val now = SystemClock.elapsedRealtime()
            val dt = ((now - lastFrameMs) / 1000.0).coerceIn(0.0, 0.1)
            lastFrameMs = now
            val blocked = inputBlockedReason(now)
            if (blocked != null) running = false
            val angle = if (useVehicleInput) vehicleInput.steering.value ?: 0f else screenSteering
            val brake = if (useVehicleInput) vehicleInput.brake.value ?: true else brakeHeld
            val throttle = if (useVehicleInput) avdPedals.throttle(now) else if (gasHeld) 1f else 0f
            simulation.step(SimulationInput(angle, throttle, brake, running && blocked == null), dt)
            if (simulation.collided || simulation.parked) running = false
            parkingView.render(angle, running)
            if (now - lastUiMs >= 200L) {
                refreshUi(now)
                lastUiMs = now
            }
            handler.postDelayed(this, 33L)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        supportActionBar?.hide()
        buildScreen()
        source = PleosVehicleInput(this) { vehicleInput = it }
        pedalSource = AvdConsolePedals { avdPedals = it }
        requestVehiclePermissions()
    }

    override fun onStart() {
        super.onStart()
        foreground = true
        source.start()
        pedalSource.start()
        lastFrameMs = SystemClock.elapsedRealtime()
        handler.post(frame)
    }

    override fun onStop() {
        foreground = false
        handler.removeCallbacks(frame)
        pause()
        source.stop()
        pedalSource.stop()
        super.onStop()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (!hasFocus) pause()
    }

    private fun pause() {
        running = false
        gasHeld = false
        brakeHeld = false
        simulation.stop()
    }

    private fun buildScreen() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(18), dp(12), dp(18), dp(12))
            setBackgroundColor(ink)
        }
        setContentView(root)

        val header = LinearLayout(this).apply { gravity = Gravity.CENTER_VERTICAL }
        val title = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        title.addView(label("PARKING LAB", 23f, white, true))
        title.addView(label("Pleos 주차 시뮬레이터 · 프로토타입", 12f, muted))
        header.addView(title, LinearLayout.LayoutParams(0, dp(58), 1f))
        liveModeButton = button("AVD 입력") { changeMode(true) }
        demoModeButton = button("화면 입력") { changeMode(false) }
        header.addView(liveModeButton, sized(144, 44))
        header.addView(demoModeButton, sized(108, 44))
        header.addView(button("다시 연결") {
            pause()
            source.stop()
            source.start()
            pedalSource.stop()
            pedalSource.start()
            requestVehiclePermissions()
        }, sized(96, 44))
        root.addView(header)

        val mainRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        parkingView = ParkingView(this, simulation)
        mainRow.addView(parkingView, LinearLayout.LayoutParams(0, -1, 1f).apply { rightMargin = dp(12) })
        val telemetry = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16), dp(14), dp(16), dp(14))
            background = rounded(panel)
        }
        telemetry.addView(label("AVD 입력 데이터", 17f, white, true))
        telemetry.addView(label("PC 페달 콘솔 :8090 에서 조작하세요", 11f, muted))
        statusText = label("입력을 연결하는 중", 13f, mint).apply {
            setPadding(0, dp(12), 0, dp(12))
            accessibilityLiveRegion = View.ACCESSIBILITY_LIVE_REGION_POLITE
        }
        telemetry.addView(statusText)
        for (key in listOf("조향각", "브레이크", "AVD 기어", "AVD 속도", "RPM", "가속페달", "브레이크 비율")) {
            val text = label("$key  —", 13f, white).apply {
                setPadding(0, dp(6), 0, dp(6))
                minHeight = dp(42)
            }
            signalViews[key] = text
            telemetry.addView(text)
        }
        telemetry.addView(label("조향은 전륜 각도 기준입니다.\nRPM은 진단 표시용이며 가속량으로 환산하지 않습니다.", 11f, muted).apply {
            setPadding(0, dp(12), 0, 0)
        })
        val scroll = ScrollView(this).apply {
            isFillViewport = true
            addView(telemetry)
        }
        mainRow.addView(scroll, LinearLayout.LayoutParams(dp(296), -1))
        root.addView(mainRow, LinearLayout.LayoutParams(-1, 0, 1f).apply { topMargin = dp(8); bottomMargin = dp(12) })

        val controls = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(14), dp(8), dp(14), dp(10))
            background = rounded(panel)
        }
        val upper = LinearLayout(this).apply { gravity = Gravity.CENTER_VERTICAL }
        simulationText = label("가상 R · 0.0 km/h", 16f, white, true)
        upper.addView(simulationText, LinearLayout.LayoutParams(0, dp(35), 1f))
        accelerationText = label("가속 입력: AVD 페달", 12f, muted)
        upper.addView(accelerationText)
        controls.addView(upper)

        val controlRow = LinearLayout(this).apply { gravity = Gravity.CENTER_VERTICAL }
        val steeringColumn = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        steeringLabel = label("조향 입력 연결 중", 12f, muted)
        steeringColumn.addView(steeringLabel)
        steeringBar = SeekBar(this).apply {
            max = 700
            progress = 350
            contentDescription = "화면 조향각"
            setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) {
                    screenSteering = (progress - 350) / 10f
                }
                override fun onStartTrackingTouch(seekBar: SeekBar?) = Unit
                override fun onStopTrackingTouch(seekBar: SeekBar?) = Unit
            })
        }
        steeringColumn.addView(steeringBar, LinearLayout.LayoutParams(-1, dp(38)))
        controlRow.addView(steeringColumn, LinearLayout.LayoutParams(0, dp(64), 1f).apply { rightMargin = dp(8) })
        controlRow.addView(button("중앙") { steeringBar.progress = 350 }, sized(60, 52))
        driveButton = button("가상 D") { selectGear(VirtualGear.D) }
        reverseButton = button("가상 R") { selectGear(VirtualGear.R) }
        controlRow.addView(driveButton, sized(78, 52))
        controlRow.addView(reverseButton, sized(78, 52))
        gasButton = button("가속\n누르는 동안") {}.apply {
            contentDescription = "화면 가속 버튼. 누르는 동안 가속"
            setOnTouchListener { buttonView, event ->
                when (event.actionMasked) {
                    MotionEvent.ACTION_DOWN -> { gasHeld = running; isPressed = true; true }
                    MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                        gasHeld = false
                        isPressed = false
                        if (event.actionMasked == MotionEvent.ACTION_UP) buttonView.performClick()
                        true
                    }
                    else -> true
                }
            }
        }
        brakeButton = button("브레이크\n화면 입력용") {}.apply {
            contentDescription = "화면 입력 모드의 브레이크. 누르는 동안 제동"
            setOnTouchListener { buttonView, event ->
                when (event.actionMasked) {
                    MotionEvent.ACTION_DOWN -> { brakeHeld = true; gasHeld = false; isPressed = true; true }
                    MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                        brakeHeld = false
                        isPressed = false
                        if (event.actionMasked == MotionEvent.ACTION_UP) buttonView.performClick()
                        true
                    }
                    else -> true
                }
            }
        }
        controlRow.addView(gasButton, sized(102, 52))
        controlRow.addView(brakeButton, sized(112, 52))
        startButton = button("시작") {
            if (running) pause()
            else {
                val reason = inputBlockedReason(SystemClock.elapsedRealtime())
                if (reason == null && !simulation.collided && !simulation.parked) running = true
                else Toast.makeText(this, reason ?: "초기화 후 다시 시작하세요", Toast.LENGTH_SHORT).show()
            }
            refreshUi(SystemClock.elapsedRealtime())
        }
        controlRow.addView(startButton, sized(80, 52))
        controlRow.addView(button("초기화") {
            pause()
            simulation.reset()
            steeringBar.progress = 350
            refreshUi(SystemClock.elapsedRealtime())
        }, sized(80, 52))
        controls.addView(controlRow)
        root.addView(controls)
        refreshUi(SystemClock.elapsedRealtime())
    }

    private fun changeMode(useVehicle: Boolean) {
        pause()
        useVehicleInput = useVehicle
        refreshUi(SystemClock.elapsedRealtime())
    }

    private fun inputBlockedReason(now: Long): String? = if (useVehicleInput)
        vehicleInput.blockedReason(now) ?: avdPedals.blockedReason(now) else null

    private fun selectGear(next: VirtualGear) {
        if (!simulation.selectGear(next)) {
            Toast.makeText(this, "화면 속 자동차를 정지한 뒤 기어를 바꿔 주세요", Toast.LENGTH_SHORT).show()
        }
        refreshUi(SystemClock.elapsedRealtime())
    }

    private fun refreshUi(now: Long) {
        val blocked = inputBlockedReason(now)
        setTextIfChanged(statusText, when {
            !useVehicleInput -> "화면 입력 모드 · AVD 데이터는 참고 표시"
            blocked != null -> blocked
            else -> "입력 연결됨 · P단 / 정차 확인"
        })
        statusText.setTextColor(if (useVehicleInput && blocked != null) Color.rgb(246, 195, 112) else mint)
        showSignal("조향각", vehicleInput.steering, now) { format("%+.1f°", it) }
        showSignal("브레이크", vehicleInput.brake, now) { if (it) "눌림" else "안 눌림" }
        showSignal("AVD 기어", vehicleInput.gear, now) {
            when (it) { 1 -> "N"; 2 -> "R"; 4 -> "P"; 8 -> "D"; else -> "기타 ($it)" }
        }
        showSignal("AVD 속도", vehicleInput.speed, now) { format("%.1f km/h", abs(it) * 3.6f) }
        showSignal("RPM", vehicleInput.rpm, now) { format("%.0f rpm", it) }
        showPedal("가속페달", avdPedals.acceleratorPercent, now)
        showPedal("브레이크 비율", avdPedals.brakePercent, now)
        setTextIfChanged(simulationText, format("가상 %s  ·  %.1f km/h  ·  %s", simulation.gear.name,
            abs(simulation.speedMps) * 3.6, when {
                simulation.collided -> "충돌"
                simulation.parked -> "주차 성공"
                running -> "연습 중"
                else -> "대기"
            }))
        setTextIfChanged(steeringLabel, if (useVehicleInput) "차량 조향 사용 · 화면 조향 잠금" else
            format("화면 조향  %+.1f°", screenSteering))
        steeringBar.isEnabled = !useVehicleInput
        brakeButton.isEnabled = !useVehicleInput
        gasButton.isEnabled = !useVehicleInput && running
        gasButton.alpha = if (gasButton.isEnabled) 1f else 0.45f
        setTextIfChanged(gasButton, if (useVehicleInput) "콘솔 ACCEL\n입력 사용" else "가속\n누르는 동안")
        setTextIfChanged(startButton, if (running) "일시정지" else "시작")
        startButton.isEnabled = blocked == null && !simulation.collided && !simulation.parked
        highlight(startButton, running)
        highlight(liveModeButton, useVehicleInput)
        highlight(demoModeButton, !useVehicleInput)
        highlight(driveButton, simulation.gear == VirtualGear.D)
        highlight(reverseButton, simulation.gear == VirtualGear.R)
        setTextIfChanged(accelerationText, if (useVehicleInput)
            format("가속 입력: AVD 페달  %.0f%%", avdPedals.throttle(now) * 100f)
            else "가속 입력: 화면 버튼")
    }

    private fun showPedal(key: String, value: Float?, now: Long) {
        val error = avdPedals.blockedReason(now)
        val text = if (value != null && error == null) format("%s  %.1f %%", key, value)
            else "$key  —\n${error ?: avdPedals.note}"
        signalViews[key]?.let { setTextIfChanged(it, text) }
    }

    private fun <T> showSignal(key: String, signal: VehicleSignal<T>, now: Long, valueText: (T) -> String) {
        val value = signal.value
        val text = when {
            value == null -> "$key  —\n${signal.note}"
            !signal.isFresh(now) -> "$key  —\n값 갱신이 끊겼어요"
            else -> "$key  ${valueText(value)}"
        }
        signalViews[key]?.let { setTextIfChanged(it, text) }
    }

    private fun requestVehiclePermissions() {
        val permissions = listOf("android.car.permission.CAR_SPEED",
            "android.car.permission.READ_CAR_PEDALS", "android.car.permission.CAR_ENGINE_DETAILED_3P")
        val missing = permissions.filter { name ->
            val info = runCatching { packageManager.getPermissionInfo(name, 0) }.getOrNull()
            info != null && info.protection == PermissionInfo.PROTECTION_DANGEROUS &&
                checkSelfPermission(name) != PackageManager.PERMISSION_GRANTED
        }
        if (missing.isNotEmpty()) requestPermissions(missing.toTypedArray(), 42)
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 42 && foreground) {
            source.stop()
            source.start()
        }
    }

    private fun label(value: String, size: Float, color: Int, bold: Boolean = false) = TextView(this).apply {
        text = value
        textSize = size * uiScale
        setTextColor(color)
        if (bold) typeface = Typeface.create("sans-serif-medium", Typeface.BOLD)
    }

    private fun button(value: String, action: () -> Unit) = Button(this).apply {
        text = value
        textSize = 12f * uiScale
        isAllCaps = false
        minWidth = 0
        minimumWidth = 0
        minHeight = 0
        minimumHeight = 0
        setPadding(dp(6), 0, dp(6), 0)
        setTextColor(white)
        background = rounded(Color.rgb(34, 51, 72))
        setOnClickListener { action() }
    }

    private fun highlight(button: Button, selected: Boolean) {
        if (button.tag != selected) {
            button.tag = selected
            button.background = rounded(if (selected) mint else Color.rgb(34, 51, 72))
            button.setTextColor(if (selected) ink else white)
        }
        button.alpha = if (button.isEnabled) 1f else 0.45f
    }

    private fun setTextIfChanged(view: TextView, value: String) {
        if (view.text.toString() != value) view.text = value
    }

    private fun rounded(color: Int) = GradientDrawable().apply { setColor(color); cornerRadius = dp(12).toFloat() }
    private fun sized(width: Int, height: Int) = LinearLayout.LayoutParams(dp(width), dp(height)).apply { leftMargin = dp(7) }
    private val uiScale: Float
        get() = (resources.displayMetrics.widthPixels / resources.displayMetrics.density / 1280f).coerceIn(0.85f, 1.8f)
    private fun dp(value: Int) = (value * resources.displayMetrics.density * uiScale).toInt()
    private fun format(value: String, vararg args: Any) = String.format(Locale.KOREA, value, *args)
}
