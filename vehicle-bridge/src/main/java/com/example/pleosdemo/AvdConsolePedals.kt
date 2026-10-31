package com.example.pleosdemo

import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledExecutorService
import java.util.concurrent.TimeUnit

/** AVD 안에서 실행 중인 Virtual Console 서버의 페달 값을 읽는다. */
class AvdConsolePedals(private val onUpdate: (AvdPedals) -> Unit) {
    private val main = Handler(Looper.getMainLooper())
    @Volatile private var generation = 0
    @Volatile private var connection: HttpURLConnection? = null
    private var executor: ScheduledExecutorService? = null

    fun start() {
        if (executor != null) return
        val run = ++generation
        onUpdate(AvdPedals())
        executor = Executors.newSingleThreadScheduledExecutor { task ->
            Thread(task, "avd-console-pedals").apply { isDaemon = true }
        }.also { service ->
            service.scheduleWithFixedDelay({
                val result = readPedals()
                main.post { if (generation == run) onUpdate(result) }
            }, 0L, 100L, TimeUnit.MILLISECONDS)
        }
    }

    private fun readPedals(): AvdPedals {
        val request = URL("http://127.0.0.1:8088/vehicle/state").openConnection() as HttpURLConnection
        connection = request
        return try {
            request.connectTimeout = 300
            request.readTimeout = 300
            request.useCaches = false
            if (request.responseCode != 200) return AvdPedals(note = "AVD 콘솔 서버 응답을 확인하세요")
            val state = JSONObject(request.inputStream.bufferedReader(Charsets.UTF_8).use { it.readText() })
            val controls = state.optJSONObject("controls")
                ?: return AvdPedals(note = "AVD 페달 데이터가 없어요")
            val browserActive = state.optJSONObject("browser")?.optBoolean("active", false) == true
            if (!browserActive || !controls.optBoolean("emulator_active", false)) {
                return AvdPedals(note = "PC에서 페달 콘솔(8090)을 열어 주세요")
            }
            AvdPedals(
                acceleratorPercent = controls.optDouble("accelerator_pct", Double.NaN).toFloat(),
                brakePercent = controls.optDouble("brake_pct", Double.NaN).toFloat(),
                receivedAtMs = SystemClock.elapsedRealtime(),
                note = "AVD 콘솔",
            )
        } catch (_: Exception) {
            AvdPedals(note = "AVD 콘솔 연결을 확인하세요")
        } finally {
            request.disconnect()
            connection = null
        }
    }

    fun stop() {
        generation++
        executor?.shutdownNow()
        executor = null
        connection?.disconnect()
        connection = null
        main.removeCallbacksAndMessages(null)
    }
}
