package com.notificationbridge.app

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.provider.Settings
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView

// Developer-only notification inspector for Phase 1 verification; not part of the shipped
// product UI (that's the Windows overlay, built in a later phase).
class MainActivity : Activity() {

    private lateinit var listContainer: LinearLayout
    private val onNotificationsChanged = { runOnUiThread { renderNotifications() } }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 32, 32, 32)
        }

        root.addView(TextView(this).apply {
            text = "Notification Bridge — Dev Inspector"
            textSize = 20f
        })

        root.addView(Button(this).apply {
            text = "Open notification access settings"
            setOnClickListener {
                startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS))
            }
        })

        listContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        val scroll = ScrollView(this)
        scroll.addView(listContainer)
        root.addView(scroll)

        setContentView(root)
    }

    override fun onResume() {
        super.onResume()
        NotificationRepository.addListener(onNotificationsChanged)
        renderNotifications()
    }

    override fun onPause() {
        super.onPause()
        NotificationRepository.removeListener(onNotificationsChanged)
    }

    private fun renderNotifications() {
        listContainer.removeAllViews()
        val snapshot = NotificationRepository.snapshot()
        if (snapshot.isEmpty()) {
            listContainer.addView(TextView(this).apply { text = "No notifications captured yet." })
            return
        }
        snapshot.forEach { notification ->
            listContainer.addView(TextView(this).apply {
                text = buildString {
                    appendLine("${notification.appName} (${notification.packageName})")
                    appendLine("title: ${notification.title}")
                    appendLine("body: ${notification.body}")
                    if (notification.expandedLines.isNotEmpty()) {
                        appendLine("expanded: ${notification.expandedLines}")
                    }
                    appendLine("category: ${notification.category}  key: ${notification.key}")
                }
                setPadding(0, 16, 0, 16)
                setTextColor(Color.BLACK)
            })
        }
    }
}
