package com.notificationbridge.app

import android.content.Context

data class TrustedPc(
    val host: String,
    val port: Int,
    val certFingerprint: String,
    val deviceId: String,
    val sharedSecretBase64: String,
)

// Persists the paired PC's identity per SECURITY.md #2 ("pairing should create a persistent
// trust relationship") so the app doesn't need re-pairing on every restart, per PRODUCT.md
// section 5. SharedPreferences is enough for a handful of small string values -- no database
// needed for one trusted PC.
object TrustedPcStore {
    private const val PREFS_NAME = "trusted_pc"
    private const val KEY_HOST = "host"
    private const val KEY_PORT = "port"
    private const val KEY_FINGERPRINT = "fingerprint"
    private const val KEY_DEVICE_ID = "device_id"
    private const val KEY_SECRET = "secret"

    private fun prefs(context: Context) =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    fun save(context: Context, pc: TrustedPc) {
        prefs(context).edit()
            .putString(KEY_HOST, pc.host)
            .putInt(KEY_PORT, pc.port)
            .putString(KEY_FINGERPRINT, pc.certFingerprint)
            .putString(KEY_DEVICE_ID, pc.deviceId)
            .putString(KEY_SECRET, pc.sharedSecretBase64)
            .apply()
    }

    fun load(context: Context): TrustedPc? {
        val p = prefs(context)
        val host = p.getString(KEY_HOST, null) ?: return null
        val fingerprint = p.getString(KEY_FINGERPRINT, null) ?: return null
        val deviceId = p.getString(KEY_DEVICE_ID, null) ?: return null
        val secret = p.getString(KEY_SECRET, null) ?: return null
        val port = p.getInt(KEY_PORT, -1)
        if (port <= 0) return null
        return TrustedPc(host, port, fingerprint, deviceId, secret)
    }

    fun clear(context: Context) {
        prefs(context).edit().clear().apply()
    }
}
