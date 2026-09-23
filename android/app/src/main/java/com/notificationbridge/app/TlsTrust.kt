package com.notificationbridge.app

import java.security.MessageDigest
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocketFactory
import javax.net.ssl.X509TrustManager

// Trust-on-first-use certificate pinning: the PC's TLS certificate is self-signed (see
// ADR-009/ADR-010), so there is no certificate authority to validate against. Trust is instead
// established once during pairing (via the pairing code) and pinned by fingerprint from then on.
object TlsTrust {

    fun fingerprint(cert: X509Certificate): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(cert.encoded)
        return digest.joinToString("") { "%02X".format(it) }
    }

    // Accepts any certificate. Used ONLY during the pairing exchange itself, where trust comes
    // from the pairing code (out-of-band), never for a normal authenticated reconnect.
    fun trustAllSocketFactory(): Pair<SSLSocketFactory, X509TrustManager> {
        val trustManager = object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
            override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
            override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
        }
        val context = SSLContext.getInstance("TLS")
        context.init(null, arrayOf(trustManager), null)
        return context.socketFactory to trustManager
    }

    // Pins to exactly the certificate fingerprint captured during pairing.
    fun pinnedSocketFactory(expectedFingerprint: String): Pair<SSLSocketFactory, X509TrustManager> {
        val trustManager = object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
            override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {
                val cert = chain?.firstOrNull()
                    ?: throw CertificateException("No server certificate presented")
                if (fingerprint(cert) != expectedFingerprint) {
                    throw CertificateException("Server certificate fingerprint mismatch")
                }
            }
            override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
        }
        val context = SSLContext.getInstance("TLS")
        context.init(null, arrayOf(trustManager), null)
        return context.socketFactory to trustManager
    }
}
