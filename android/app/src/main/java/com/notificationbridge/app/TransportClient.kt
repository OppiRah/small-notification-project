package com.notificationbridge.app

import android.content.Context
import android.os.Handler
import android.os.Looper
import java.util.Base64
import java.util.UUID
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.TimeUnit
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONObject

// Connects to the PC paired via PairingClient, over TLS pinned to that PC's certificate
// fingerprint (ADR-009), and authenticates every connection with the shared secret established
// during pairing (ADR-010) -- notifications are only ever sent once AUTH_RESULT succeeds.
object TransportClient {

    enum class State { NOT_PAIRED, DISCONNECTED, CONNECTING, AUTHENTICATING, CONNECTED }

    private const val TAG = "NotificationBridge"
    private const val INITIAL_BACKOFF_MS = 1_000L
    private const val MAX_BACKOFF_MS = 30_000L

    // Without pings OkHttp never notices a half-open connection (Wi-Fi dropped, PC asleep), so
    // send() would keep "succeeding" into a dead socket. A missed pong fails the connection,
    // which triggers the normal reconnect path.
    private const val PING_INTERVAL_SECONDS = 15L

    private val handler = Handler(Looper.getMainLooper())
    private val listeners = CopyOnWriteArrayList<(State) -> Unit>()

    private var webSocket: WebSocket? = null
    private var backoffMs = INITIAL_BACKOFF_MS
    private var manuallyStopped = true

    // Read from the listener service's binder thread in send(), written from OkHttp threads.
    @Volatile
    private var authenticated = false

    // Identifies the current connection attempt so callbacks from a superseded socket (e.g. the
    // old one being cancelled during a restart) can't tear down or reschedule the new one.
    @Volatile
    private var generation = 0

    @Volatile
    var state: State = State.NOT_PAIRED
        private set

    fun addStateListener(listener: (State) -> Unit) = listeners.add(listener)
    fun removeStateListener(listener: (State) -> Unit) = listeners.remove(listener)

    fun start(context: Context) {
        val pc = TrustedPcStore.load(context.applicationContext)
        if (pc == null) {
            setState(State.NOT_PAIRED)
            return
        }

        manuallyStopped = false
        backoffMs = INITIAL_BACKOFF_MS
        connect(pc)
    }

    fun stop() {
        manuallyStopped = true
        authenticated = false
        handler.removeCallbacksAndMessages(null)
        webSocket?.close(1000, "client stopping")
        webSocket = null
        setState(State.DISCONNECTED)
    }

    fun send(json: String) {
        val sent = authenticated && (webSocket?.send(json) ?: false)
        if (!sent) {
            BridgeLogger.w(TAG, "Dropped message: transport not authenticated")
        }
    }

    private fun connect(pc: TrustedPc) {
        // start() can be called while a connection already exists (MainActivity and the listener
        // service both call it); drop the old socket rather than leaking a second live one.
        val thisGeneration = ++generation
        webSocket?.cancel()
        setState(State.CONNECTING)

        val (socketFactory, trustManager) = TlsTrust.pinnedSocketFactory(pc.certFingerprint)
        val client = OkHttpClient.Builder()
            .sslSocketFactory(socketFactory, trustManager)
            .pingInterval(PING_INTERVAL_SECONDS, TimeUnit.SECONDS)
            // Identity is verified by exact certificate fingerprint pinning above, a stronger
            // guarantee than hostname/CN matching -- the self-signed cert has no SAN for the
            // PC's LAN IP, so default hostname verification would otherwise reject it.
            .hostnameVerifier { _, _ -> true }
            .build()

        val request = Request.Builder().url("wss://${pc.host}:${pc.port}/ws/").build()
        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                if (thisGeneration != generation) return
                setState(State.AUTHENTICATING)
                sendAuthenticate(webSocket, pc)
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                if (thisGeneration != generation) return
                handleMessage(pc, text)
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                if (thisGeneration != generation) return
                BridgeLogger.w(TAG, "Transport connection failed: ${t.message}")
                scheduleReconnect(pc)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                if (thisGeneration != generation) return
                BridgeLogger.w(TAG, "Transport closed: $reason")
                scheduleReconnect(pc)
            }
        })
    }

    private fun sendAuthenticate(webSocket: WebSocket, pc: TrustedPc) {
        val nonce = UUID.randomUUID().toString()
        val timestamp = ProtocolMessages.nowIsoTimestamp()
        val secret = Base64.getDecoder().decode(pc.sharedSecretBase64)

        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(secret, "HmacSHA256"))
        val message = "${pc.deviceId}|$nonce|$timestamp"
        val proof = mac.doFinal(message.toByteArray(Charsets.UTF_8))
        val proofBase64 = Base64.getEncoder().encodeToString(proof)

        webSocket.send(ProtocolMessages.authenticate(pc.deviceId, nonce, timestamp, proofBase64))
    }

    private fun handleMessage(pc: TrustedPc, text: String) {
        val json = try {
            JSONObject(text)
        } catch (e: org.json.JSONException) {
            return
        }
        if (json.optString("messageType") != "AUTH_RESULT") return

        val payload = json.optJSONObject("payload") ?: JSONObject()
        if (payload.optBoolean("success", false)) {
            authenticated = true
            backoffMs = INITIAL_BACKOFF_MS
            setState(State.CONNECTED)
            BridgeLogger.i(TAG, "Transport authenticated")
        } else {
            authenticated = false
            BridgeLogger.w(TAG, "Authentication rejected by PC")
            webSocket?.close(1000, "auth rejected")
            scheduleReconnect(pc)
        }
    }

    private fun scheduleReconnect(pc: TrustedPc) {
        authenticated = false
        setState(State.DISCONNECTED)
        if (manuallyStopped) return
        // A failure and the close that follows it (or an auth rejection and its close) both land
        // here; keep only one reconnect pending.
        handler.removeCallbacksAndMessages(null)
        handler.postDelayed({ if (!manuallyStopped) connect(pc) }, backoffMs)
        backoffMs = (backoffMs * 2).coerceAtMost(MAX_BACKOFF_MS)
    }

    private fun setState(newState: State) {
        state = newState
        listeners.forEach { it(newState) }
    }
}
