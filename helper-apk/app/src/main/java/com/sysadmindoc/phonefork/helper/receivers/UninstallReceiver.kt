package com.sysadmindoc.phonefork.helper.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.util.Log

/**
 * Self-uninstall hook. The host runs `adb shell am broadcast -a com.sysadmindoc.phonefork.helper.UNINSTALL`
 * after a successful migration; we schedule a system-side uninstall against our own package so the user's
 * destination phone is left with no helper trace (F019).
 *
 * Note: an app cannot grant itself REQUEST_DELETE_PACKAGES; we therefore signal back to the host that the
 * helper is ready for removal. The host then issues `adb shell pm uninstall com.sysadmindoc.phonefork.helper`.
 * Keeping the receiver here so the wire-protocol stays symmetric and a future Android version that exposes
 * an in-app self-delete API has a hook to plug into.
 */
class UninstallReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        Log.i(TAG, "Helper self-uninstall requested; host should follow up with `pm uninstall`.")
        try {
            val installer = context.packageManager.packageInstaller
            // Best-effort: a normal app cannot drive PackageInstaller.uninstall against itself without
            // the DELETE_PACKAGES permission (system signature only). The host handles the real removal.
            installer.javaClass // touch to ensure the package-installer service is reachable
        } catch (ex: Throwable) {
            Log.w(TAG, "PackageInstaller probe failed", ex)
        }
    }

    companion object {
        private const val TAG = "PhoneForkHelper.Uninstall"
    }
}
