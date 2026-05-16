package com.sysadmindoc.phonefork.helper.providers

/**
 * One concrete ContentProvider per migration category. Each binds an authority
 * declared in AndroidManifest.xml and inherits the shell-UID gate + JSON envelope
 * from BaseHelperProvider. Real body implementations land in v0.7.1+ once the
 * Windows host's wire-protocol contract is locked.
 */

class SmsProvider : BaseHelperProvider() {
    override val authorityName = "sms"
}

class CallLogProvider : BaseHelperProvider() {
    override val authorityName = "calllog"
}

class ContactsProvider : BaseHelperProvider() {
    override val authorityName = "contacts"
}

class WifiProvider : BaseHelperProvider() {
    override val authorityName = "wifi"
}

class WallpaperProvider : BaseHelperProvider() {
    override val authorityName = "wallpaper"
}

class RingtoneProvider : BaseHelperProvider() {
    override val authorityName = "ringtone"
}

class DictionaryProvider : BaseHelperProvider() {
    override val authorityName = "dictionary"
}
