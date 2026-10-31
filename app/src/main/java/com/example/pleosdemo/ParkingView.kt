package com.example.pleosdemo

import android.content.Context
import android.graphics.*
import android.view.View
import kotlin.math.*

class ParkingView(context: Context, private val simulation: ParkingSimulation) : View(context) {
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val path = Path()
    private val targetDash = DashPathEffect(floatArrayOf(0.3f, 0.18f), 0f)
    private val predictionDash = DashPathEffect(floatArrayOf(0.2f, 0.14f), 0f)
    private val font = Typeface.create("sans-serif-medium", Typeface.NORMAL)
    private var worldScale = 1f
    private var drawnPose: CarPose? = null
    private var drawnCollision = false
    private var drawnParked = false
    var steeringDegrees = 0f
    var running = false
    private val mint = Color.rgb(86, 231, 188)

    init { contentDescription = "평행 주차 시뮬레이션. 초록색 목표 구역과 화면 속 자동차" }

    fun render(angle: Float, isRunning: Boolean) {
        if (drawnPose != simulation.pose || steeringDegrees != angle || running != isRunning ||
            drawnCollision != simulation.collided || drawnParked != simulation.parked) {
            steeringDegrees = angle
            running = isRunning
            drawnPose = simulation.pose
            drawnCollision = simulation.collided
            drawnParked = simulation.parked
            invalidate()
        }
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        canvas.drawColor(Color.rgb(16, 26, 40))
        val margin = 22f * resources.displayMetrics.density
        val scale = min((width - margin * 2) / 23f, (height - margin * 2) / 13f).coerceAtLeast(1f)
        worldScale = scale
        canvas.save()
        canvas.translate((width - 23f * scale) / 2f, (height - 13f * scale) / 2f)
        canvas.scale(scale, scale)
        canvas.clipRect(0f, 0f, 23f, 13f)

        paint.style = Paint.Style.STROKE
        paint.strokeWidth = 0.018f
        paint.color = Color.rgb(28, 42, 59)
        for (x in 0..23) canvas.drawLine(x.toFloat(), 0f, x.toFloat(), 13f, paint)
        for (y in 0..13) canvas.drawLine(0f, y.toFloat(), 23f, y.toFloat(), paint)

        paint.style = Paint.Style.FILL
        paint.color = Color.rgb(43, 55, 70)
        canvas.drawRect(0f, 0f, 23f, 0.55f, paint)
        paint.color = Color.rgb(232, 191, 94)
        canvas.drawRect(0f, 0.5f, 23f, 0.57f, paint)
        val target = ParkingSimulation.TARGET
        paint.color = Color.argb(28, 86, 231, 188)
        canvas.drawRoundRect(rect(target), 0.12f, 0.12f, paint)
        paint.style = Paint.Style.STROKE
        paint.strokeWidth = 0.055f
        paint.color = mint
        paint.pathEffect = targetDash
        canvas.drawRoundRect(rect(target), 0.12f, 0.12f, paint)
        paint.pathEffect = null
        text(canvas, "PARKING", 11.1f, 2.56f, 0.42f, mint)

        ParkingSimulation.OBSTACLES.forEach {
            drawCar(canvas, (it.left + it.right).toFloat() / 2f,
                (it.top + it.bottom).toFloat() / 2f, 0f, Color.rgb(89, 108, 132), false)
        }

        drawPrediction(canvas)
        val center = simulation.center()
        drawCar(canvas, center.x.toFloat(), center.y.toFloat(),
            Math.toDegrees(simulation.pose.heading).toFloat(),
            if (simulation.collided) Color.rgb(252, 118, 123) else mint, true)

        val label = when {
            simulation.collided -> "충돌했어요 · 초기화 후 다시 시도하세요"
            simulation.parked -> "주차 성공!"
            running -> "R로 후진하며 목표 구역에 진입해 보세요"
            else -> "AVD는 P단 / 정차 유지 · 화면 속 자동차만 움직여요"
        }
        text(canvas, label, 11.5f, 11.7f, 0.37f, Color.rgb(192, 207, 222))
        canvas.restore()
    }

    private fun drawPrediction(canvas: Canvas) {
        if (simulation.collided || simulation.parked) return
        var pose = simulation.pose
        val step = 0.25 * simulation.gear.direction
        val angle = Math.toRadians(steeringDegrees.coerceIn(-35f, 35f).toDouble())
        path.reset()
        for (i in 0..22) {
            val center = simulation.center(pose)
            if (i == 0) path.moveTo(center.x.toFloat(), center.y.toFloat())
            else path.lineTo(center.x.toFloat(), center.y.toFloat())
            val yaw = step / ParkingSimulation.WHEELBASE * tan(angle)
            pose = CarPose(pose.rearX + step * cos(pose.heading + yaw / 2),
                pose.rearY + step * sin(pose.heading + yaw / 2), pose.heading + yaw)
        }
        paint.style = Paint.Style.STROKE
        paint.color = Color.argb(160, 130, 191, 255)
        paint.strokeWidth = 0.045f
        paint.pathEffect = predictionDash
        canvas.drawPath(path, paint)
        paint.pathEffect = null
    }

    private fun drawCar(canvas: Canvas, x: Float, y: Float, heading: Float, color: Int, controlled: Boolean) {
        canvas.save()
        canvas.translate(x, y)
        canvas.rotate(heading)
        paint.style = Paint.Style.FILL
        paint.color = Color.argb(90, 0, 0, 0)
        canvas.drawRoundRect(-2.22f, -0.8f, 2.42f, 1f, 0.3f, 0.3f, paint)
        for (axle in listOf(-1.35f, 1.35f)) {
            for (side in listOf(-0.9f, 0.9f)) {
                canvas.save()
                canvas.translate(axle, side)
                if (axle > 0f && controlled) canvas.rotate(steeringDegrees.coerceIn(-35f, 35f))
                paint.color = Color.rgb(5, 11, 18)
                canvas.drawRoundRect(-0.34f, -0.13f, 0.34f, 0.13f, 0.06f, 0.06f, paint)
                canvas.restore()
            }
        }
        paint.color = color
        canvas.drawRoundRect(-2.3f, -0.9f, 2.3f, 0.9f, 0.28f, 0.28f, paint)
        paint.color = Color.rgb(23, 42, 58)
        canvas.drawRoundRect(-0.9f, -0.7f, 1.15f, 0.7f, 0.2f, 0.2f, paint)
        paint.color = color
        canvas.drawRoundRect(-0.7f, -0.56f, 0.62f, 0.56f, 0.13f, 0.13f, paint)
        paint.color = Color.rgb(236, 247, 230)
        canvas.drawRoundRect(2.1f, -0.72f, 2.25f, -0.36f, 0.03f, 0.03f, paint)
        canvas.drawRoundRect(2.1f, 0.36f, 2.25f, 0.72f, 0.03f, 0.03f, paint)
        paint.color = Color.rgb(237, 105, 110)
        canvas.drawRect(-2.28f, -0.74f, -2.16f, -0.38f, paint)
        canvas.drawRect(-2.28f, 0.38f, -2.16f, 0.74f, paint)
        canvas.restore()
    }

    private fun rect(box: ParkingBox) = RectF(box.left.toFloat(), box.top.toFloat(), box.right.toFloat(), box.bottom.toFloat())

    private fun text(canvas: Canvas, value: String, x: Float, y: Float, size: Float, color: Int) {
        canvas.save()
        canvas.translate(x, y)
        // 글자는 실제 픽셀 크기로 그려 작은 월드 좌표에서 자간이 뭉개지지 않게 한다.
        canvas.scale(1f / worldScale, 1f / worldScale)
        paint.style = Paint.Style.FILL
        paint.textAlign = Paint.Align.CENTER
        paint.typeface = font
        paint.textSize = size * worldScale
        paint.color = color
        canvas.drawText(value, 0f, 0f, paint)
        canvas.restore()
    }
}
