package com.notificationbridge.app

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.os.Build
import android.provider.Settings
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView

// Developer-only notification inspector for Phase 1 verification; not part of the shipped
// product UI (that's the Windows overlay, built in a later phase).
class MainActivity : Activity() {

    private lateinit var listContainer: LinearLayout
    private lateinit var statusText: TextView
    private lateinit var pairingStatusText: TextView
    private lateinit var hostInput: EditText
    private lateinit var portInput: EditText
    private lateinit var codeInput: EditText
    private val onNotificationsChanged = { runOnUiThread { renderNotifications() } }
    private val onTransportStateChanged = { _: TransportClient.State -> runOnUiThread { renderTransportStatus() } }

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

        statusText = TextView(this).apply {
            setPadding(0, 8, 0, 8)
        }
        root.addView(statusText)

        root.addView(Button(this).apply {
            text = "Open notification access settings"
            setOnClickListener {
                startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS))
            }
        })

        hostInput = EditText(this).apply { hint = "PC IP address" }
        root.addView(hostInput)
        portInput = EditText(this).apply { hint = "PC port (e.g. 7787)" }
        root.addView(portInput)
        codeInput = EditText(this).apply { hint = "Pairing code" }
        root.addView(codeInput)

        root.addView(LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            addView(Button(this@MainActivity).apply {
                text = "Pair"
                setOnClickListener { onPairClicked() }
            })
            addView(Button(this@MainActivity).apply {
                text = "Unpair"
                setOnClickListener { onUnpairClicked() }
            })
        })

        pairingStatusText = TextView(this).apply { setPadding(0, 4, 0, 8) }
        root.addView(pairingStatusText)

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
        TransportClient.addStateListener(onTransportStateChanged)
        renderNotifications()
        renderTransportStatus()
    }

    override fun onPause() {
        super.onPause()
        NotificationRepository.removeListener(onNotificationsChanged)
        TransportClient.removeStateListener(onTransportStateChanged)
    }

    private fun renderTransportStatus() {
        statusText.text = "Transport: ${TransportClient.state}"
    }

    private fun onPairClicked() {
        val host = hostInput.text.toString().trim()
        val port = portInput.text.toString().trim().toIntOrNull()
        val code = codeInput.text.toString().trim()

        if (host.isEmpty() || port == null || code.isEmpty()) {
            pairingStatusText.text = "Enter host, port, and code"
            return
        }

        pairingStatusText.text = "Pairing…"
        PairingClient.pair(host, port, code, Build.MODEL ?: "Android phone") { result ->
            runOnUiThread {
                when (result) {
                    is PairingClient.Result.Success -> {
                        TrustedPcStore.save(applicationContext, result.pc)
                        pairingStatusText.text = "Paired with ${result.pc.host}"
                        TransportClient.start(applicationContext)
                    }
                    is PairingClient.Result.Failure -> {
                        pairingStatusText.text = "Pairing failed: ${result.reason}"
                    }
                }
            }
        }
    }

    private fun onUnpairClicked() {
        TrustedPcStore.clear(applicationContext)
        TransportClient.stop()
        pairingStatusText.text = "Unpaired"
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
