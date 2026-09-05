// PhoneFork push-and-run agent (F115), following scrcpy's app_process pattern.
//
// app_process executes DEX, not JVM classfiles, so this module compiles plain Java against
// android.jar and then runs d8 over the classes to produce a JAR whose single entry is
// classes.dex. The result is pushed to /data/local/tmp and run as the shell user; nothing is
// installed and the only artifact is the JAR itself.
//
// Deliberately a bare java-library rather than an Android module: the agent has no manifest, no
// resources, and must carry no runtime dependency (not even the Kotlin stdlib), because whatever
// it references has to already exist on the device.

plugins {
    id("java-library")
}

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}

/** Matches the helper APK's compileSdk. */
val androidApiLevel = 36

val androidSdkDir: String = providers
    .environmentVariable("ANDROID_HOME")
    .orElse(providers.environmentVariable("ANDROID_SDK_ROOT"))
    .orElse(
        providers.provider {
            val localProperties = rootProject.file("local.properties")
            if (localProperties.exists()) {
                java.util.Properties()
                    .apply { localProperties.inputStream().use { load(it) } }
                    .getProperty("sdk.dir")
            } else {
                null
            }
        }
    )
    .orNull
    ?: error("Android SDK not found. Set ANDROID_HOME or add sdk.dir to helper-apk/local.properties.")

val androidJar = File(androidSdkDir, "platforms/android-$androidApiLevel/android.jar")

dependencies {
    // Framework classes are provided by the device at runtime, never packaged.
    compileOnly(files(androidJar))
}

/** Newest installed build-tools directory that actually contains d8. */
fun resolveD8(): File {
    val buildToolsRoot = File(androidSdkDir, "build-tools")
    val d8Name = if (System.getProperty("os.name").startsWith("Windows", ignoreCase = true)) "d8.bat" else "d8"
    val candidate = buildToolsRoot.listFiles()
        ?.filter { it.isDirectory && File(it, d8Name).exists() }
        ?.maxByOrNull { it.name }
        ?: error("No Android build-tools with $d8Name found under $buildToolsRoot.")
    return File(candidate, d8Name)
}

val dexOutputDir = layout.buildDirectory.dir("dex")

val dexClasses by tasks.registering {
    description = "Converts the compiled agent classes to classes.dex with d8."
    group = "build"
    dependsOn(tasks.named("classes"))

    val classesDirs = sourceSets.main.get().output.classesDirs
    inputs.files(classesDirs)
    inputs.file(androidJar)
    outputs.dir(dexOutputDir)

    doLast {
        val outDir = dexOutputDir.get().asFile
        outDir.mkdirs()
        val classFiles = classesDirs.asFileTree.matching { include("**/*.class") }.files
        check(classFiles.isNotEmpty()) { "No compiled classes to dex." }

        providers.exec {
            commandLine(
                buildList {
                    add(resolveD8().absolutePath)
                    add("--lib")
                    add(androidJar.absolutePath)
                    add("--min-api")
                    add("30") // matches the helper APK's minSdk
                    add("--output")
                    add(outDir.absolutePath)
                    addAll(classFiles.map { it.absolutePath })
                }
            )
        }.result.get().assertNormalExitValue()
    }
}

/**
 * The shippable artifact: a JAR containing only classes.dex, named so the host's
 * AppProcessAgentService.RemoteJarPath finds it.
 */
val agentJar by tasks.registering(Jar::class) {
    description = "Packages classes.dex into phonefork-agent.jar for app_process."
    group = "build"
    dependsOn(dexClasses)
    archiveFileName.set("phonefork-agent.jar")
    destinationDirectory.set(layout.buildDirectory.dir("agent"))
    from(dexOutputDir)
}

tasks.named("build") { dependsOn(agentJar) }
