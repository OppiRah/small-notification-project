package com.notificationbridge.app

import android.util.Log

object BridgeLogger {

    // Defaults false: notification bodies must not hit normal logs by default (SECURITY.md).
    var verbose: Boolean = false

    fun i(tag: String, message: String) = Log.i(tag, message)
    fun w(tag: String, message: String) = Log.w(tag, message)

    fun debugBody(tag: String, body: String?) {
        if (verbose) Log.d(tag, "body=$body")
    }
}
