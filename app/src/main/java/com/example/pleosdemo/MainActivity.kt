package com.example.pleosdemo

import android.os.Bundle
import android.view.Gravity
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

class MainActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val message = TextView(this).apply {
            text = "PleosDemo 실행 성공"
            textSize = 28f
            gravity = Gravity.CENTER
        }
        setContentView(message)
    }
}
