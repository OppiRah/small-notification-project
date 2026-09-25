package com.notificationbridge.app

import android.app.Notification
import android.content.ComponentName
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import android.service.notification.NotificationListenerService
import android.service.notification.StatusBarNotification

class NotificationBridgeListenerService : NotificationListenerService() {

    override fun onListenerConnected() {
        super.onListenerConnected()
        BridgeLogger.i(TAG, "Listener connected")
        TransportClient.start(applicationContext)
    }

    override fun onListenerDisconnected() {
        super.onListenerDisconnected()
        BridgeLogger.w(TAG, "Listener disconnected, requesting rebind")
        TransportClient.stop()
        requestRebind(ComponentName(applicationContext, NotificationBridgeListenerService::class.java))
    }

    override fun onNotificationPosted(sbn: StatusBarNotification) {
        // Ongoing notifications (music players, downloads) are persistent state, not transient
        // events, and are out of scope for MVP mirroring per DECISIONS.md.
        if (sbn.notification.flags and Notification.FLAG_ONGOING_EVENT != 0) {
            BridgeLogger.i(TAG, "Skipped ongoing notification package=${sbn.packageName}")
            return
        }

        // Group summaries are Android's own aggregation placeholder, not user-facing content.
        if (sbn.notification.flags and Notification.FLAG_GROUP_SUMMARY != 0) {
            BridgeLogger.i(TAG, "Skipped group summary notification package=${sbn.packageName}")
            return
        }

        // CATEGORY_SERVICE is a platform-defined signal for background/status notifications
        // (e.g. Messenger's "Chat heads active"), not user-facing content. App-agnostic per
        // ADR-003: this checks Android's own category constant, not any specific package.
        if (sbn.notification.category == Notification.CATEGORY_SERVICE) {
            BridgeLogger.i(TAG, "Skipped service-category notification package=${sbn.packageName}")
            return
        }

        val normalized = NotificationExtractor.extract(
            key = sbn.key,
            packageName = sbn.packageName,
            appName = resolveAppName(sbn.packageName),
            postTimeMillis = sbn.postTime,
            notification = sbn.notification,
        )

        BridgeLogger.i(TAG, "Notification posted package=${normalized.packageName} category=${normalized.category} key=${normalized.key}")
        NotificationRepository.upsert(normalized)
        TransportClient.send(
            ProtocolMessages.notificationPosted(normalized, AppIconProvider.pngBase64(applicationContext, sbn.packageName)),
        )
    }

    override fun onNotificationRemoved(sbn: StatusBarNotification) {
        BridgeLogger.i(TAG, "Notification removed package=${sbn.packageName}")
        NotificationRepository.remove(sbn.key)
        TransportClient.send(ProtocolMessages.notificationRemoved(sbn.key))
    }

    private fun resolveAppName(packageName: String): String {
        return try {
            val pm: PackageManager = applicationContext.packageManager
            val info: ApplicationInfo = pm.getApplicationInfo(packageName, 0)
            pm.getApplicationLabel(info).toString()
        } catch (e: PackageManager.NameNotFoundException) {
            packageName
        }
    }

    companion object {
        private const val TAG = "NotificationBridge"
    }
}
