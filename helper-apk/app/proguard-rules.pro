# Keep our ContentProviders + receivers; everything else is fair game.
-keep class com.sysadmindoc.phonefork.helper.providers.** { *; }
-keep class com.sysadmindoc.phonefork.helper.receivers.** { *; }
