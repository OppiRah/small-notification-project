package com.notificationbridge.app

import android.content.pm.ApplicationInfo
import android.content.pm.PackageInfo
import android.graphics.Color
import android.graphics.drawable.ColorDrawable
import java.util.Base64
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class AppIconProviderTest {

    private val context get() = RuntimeEnvironment.getApplication()

    @Test
    fun `installed app icon is rendered as a PNG within the size limit`() {
        val packageName = "com.example.icontest"
        val packageManager = shadowOf(context.packageManager)
        packageManager.installPackage(PackageInfo().apply {
            this.packageName = packageName
            applicationInfo = ApplicationInfo().apply { this.packageName = packageName }
        })
        packageManager.setApplicationIcon(packageName, ColorDrawable(Color.RED))

        val encoded = AppIconProvider.pngBase64(context, packageName)

        assertNotNull(encoded)
        assertTrue(encoded!!.length <= 64 * 1024)
        val bytes = Base64.getDecoder().decode(encoded)
        val pngSignature = byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
        assertEquals(pngSignature.toList(), bytes.take(8))
    }

    @Test
    fun `unknown package has no icon`() {
        assertNull(AppIconProvider.pngBase64(context, "com.does.not.exist"))
    }
}
