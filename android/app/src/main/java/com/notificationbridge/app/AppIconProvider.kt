package com.notificationbridge.app

import android.content.Context
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import java.io.ByteArrayOutputStream
import java.util.Base64
import java.util.concurrent.ConcurrentHashMap

// Renders an app's launcher icon as a small base64 PNG for the notification payload. App-agnostic
// per ADR-003: it asks PackageManager for whatever package posted the notification. Results are
// cached per package (including "no icon") because the icon is attached to every notification and
// rendering is comparatively expensive.
object AppIconProvider {

    private const val ICON_SIZE_PX = 96

    // Mirrors the PC's ProtocolLimits.IconBase64Max. An icon over the limit is dropped, never
    // allowed to make the notification itself fail.
    private const val MAX_BASE64_CHARS = 64 * 1024

    private val cache = ConcurrentHashMap<String, String>()

    fun pngBase64(context: Context, packageName: String): String? {
        val encoded = cache.getOrPut(packageName) { render(context, packageName) ?: "" }
        return encoded.ifEmpty { null }
    }

    private fun render(context: Context, packageName: String): String? {
        val drawable = try {
            context.packageManager.getApplicationIcon(packageName)
        } catch (e: PackageManager.NameNotFoundException) {
            return null
        }

        // Adaptive and vector icons aren't bitmaps; drawing onto a canvas handles every type.
        val bitmap = Bitmap.createBitmap(ICON_SIZE_PX, ICON_SIZE_PX, Bitmap.Config.ARGB_8888)
        drawable.setBounds(0, 0, ICON_SIZE_PX, ICON_SIZE_PX)
        drawable.draw(Canvas(bitmap))

        val png = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.PNG, 100, png)
        val encoded = Base64.getEncoder().encodeToString(png.toByteArray())
        return encoded.takeIf { it.length <= MAX_BASE64_CHARS }
    }
}
