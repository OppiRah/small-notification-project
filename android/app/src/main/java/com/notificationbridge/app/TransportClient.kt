package com.notificationbridge.app

import android.os.Handler
import android.os.Looper
import java.util.concurrent.CopyOnWriteArrayList
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

// Dev-only endpoint: reaches the Windows receiver over `adb reverse`, never the open LAN.
// Real device pairing/discovery is Phase 5 work (see DECISIONS.md unresolved: port/discovery).
object TransportClient {

    enum class State { DISCONNECTED, CONNECTING, CONNECTED }

    private const val TAG = "NotificationBridge"
    private const val ENDPOINT = "ws://127.0.0.1:7787/ws/"
    private const val INITIAL_BACKOFF_MS = 1_000L
    private const val MAX_BACKOFF_MS = 30_000L

    private val client = OkHttpClient()
    private val handler = Handler(Looper.getMainLooper())
    private val listeners = CopyOnWriteArrayList<(State) -> Unit>()

    private var webSocket: WebSocket? = null
    private var backoffMs = INITIAL_BACKOFF_MS
    private var manuallyStopped = true

    @Volatile
    var state: State = State.DISCONNECTED
        private set

    fun addStateListener(listener: (State) -> Unit) = listeners.add(listener)
    fun removeStateListener(listener: (State) -> Unit) = listeners.remove(listener)

    fun start() {
        manuallyStopped = false
        backoffMs = INITIAL_BACKOFF_MS
        connect()
    }

    fun stop() {
        manuallyStopped = true
        handler.removeCallbacksAndMessages(null)
        webSocket?.close(1000, "client stopping")
        webSocket = null
        setState(State.DISCONNECTED)
    }

    fun send(json: String) {
        val sent = webSocket?.send(json) ?: false
        if (!sent) {
            BridgeLogger.w(TAG, "Dropped message: transport not connected")
        }
    }

    private fun connect() {
        setState(State.CONNECTING)
        val request = Request.Builder().url(ENDPOINT).build()
        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                backoffMs = INITIAL_BACKOFF_MS
                setState(State.CONNECTED)
                BridgeLogger.i(TAG, "Transport connected")
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                BridgeLogger.w(TAG, "Transport connection failed: ${t.message}")
                scheduleReconnect()
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                BridgeLogger.w(TAG, "Transport closed: $reason")
                scheduleReconnect()
            }
        })
    }

    private fun scheduleReconnect() {
        setState(State.DISCONNECTED)
        if (manuallyStopped) return
        handler.postDelayed({ if (!manuallyStopped) connect() }, backoffMs)
        backoffMs = (backoffMs * 2).coerceAtMost(MAX_BACKOFF_MS)
    }

    private fun setState(newState: State) {
        state = newState
        listeners.forEach { it(newState) }
    }
}
