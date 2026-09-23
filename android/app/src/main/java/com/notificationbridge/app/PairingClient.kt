package com.notificationbridge.app

import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONObject
import java.security.cert.X509Certificate
import java.util.Base64
import java.util.UUID
import java.util.concurrent.TimeUnit
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec
import javax.net.ssl.SSLSocket

// One-shot pairing exchange, per ADR-009: connects with a trust-all TLS socket factory (there's
// no pinned fingerprint yet -- this connection IS how one gets established), proves it saw the
// PC's actual certificate by HMAC-signing that certificate's fingerprint with the pairing code,
// and on success persists the PC as trusted via TrustedPcStore.
object PairingClient {

    private const val TAG = "NotificationBridge"

    sealed class Result {
        data class Success(val pc: TrustedPc) : Result()
        data class Failure(val reason: String) : Result()
    }

    fun pair(host: String, port: Int, code: String, deviceName: String, onResult: (Result) -> Unit) {
        Thread {
            try {
                val cert = probeCertificate(host, port)
                if (cert == null) {
                    onResult(Result.Failure("No server certificate presented"))
                    return@Thread
                }
                val fingerprint = TlsTrust.fingerprint(cert)
                BridgeLogger.i(TAG, "Pairing: probed cert fingerprint=$fingerprint")

                val mac = Mac.getInstance("HmacSHA256")
                mac.init(SecretKeySpec(code.toByteArray(Charsets.UTF_8), "HmacSHA256"))
                val proof = mac.doFinal(fingerprint.toByteArray(Charsets.UTF_8))
                val proofBase64 = Base64.getEncoder().encodeToString(proof)

                connectAndSendPairRequest(host, port, fingerprint, proofBase64, deviceName, onResult)
            } catch (e: Exception) {
                BridgeLogger.w(TAG, "Pairing: exception during cert probe: ${e.javaClass.simpleName}: ${e.message}")
                onResult(Result.Failure("${e.javaClass.simpleName}: ${e.message}"))
            }
        }.start()
    }

    // Opens a plain TLS socket (no WebSocket/HTTP framing at all) purely to complete the TLS
    // handshake and read the server's certificate directly off the session. This sidesteps
    // OkHttp's Response.handshake / EventListener.secureConnectEnd, which were unreliable for a
    // newWebSocket() connection specifically in testing.
    private fun probeCertificate(host: String, port: Int): X509Certificate? {
        val (socketFactory, _) = TlsTrust.trustAllSocketFactory()
        (socketFactory.createSocket(host, port) as SSLSocket).use { socket ->
            socket.startHandshake()
            return socket.session.peerCertificates.filterIsInstance<X509Certificate>().firstOrNull()
        }
    }

    private fun connectAndSendPairRequest(
        host: String,
        port: Int,
        fingerprint: String,
        proofBase64: String,
        deviceName: String,
        onResult: (Result) -> Unit,
    ) {
        val (socketFactory, trustManager) = TlsTrust.trustAllSocketFactory()
        val client = OkHttpClient.Builder()
            .sslSocketFactory(socketFactory, trustManager)
            .hostnameVerifier { _, _ -> true }
            .callTimeout(15, TimeUnit.SECONDS)
            .build()

        val deviceId = UUID.randomUUID().toString()
        val request = Request.Builder().url("wss://$host:$port/ws/").build()

        client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: okhttp3.Response) {
                webSocket.send(ProtocolMessages.pairRequest(deviceId, deviceName, proofBase64))
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                BridgeLogger.i(TAG, "Pairing: message received: $text")
                val json = JSONObject(text)
                if (json.optString("messageType") != "PAIR_RESPONSE") return

                val payload = json.optJSONObject("payload") ?: JSONObject()
                if (payload.optBoolean("success", false)) {
                    onResult(Result.Success(TrustedPc(
                        host = host,
                        port = port,
                        certFingerprint = fingerprint,
                        deviceId = deviceId,
                        sharedSecretBase64 = payload.getString("sharedSecret"),
                    )))
                } else {
                    onResult(Result.Failure(payload.optString("error", "pairing failed")))
                }
                webSocket.close(1000, "pairing complete")
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: okhttp3.Response?) {
                BridgeLogger.w(TAG, "Pairing: onFailure: ${t.javaClass.simpleName}: ${t.message}")
                onResult(Result.Failure(t.message ?: "connection failed"))
            }
        })
    }
}
