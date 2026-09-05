import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.sysadmindoc.phonefork.helper"
    // Android 16 QPR2 surface; bump to 37 only after ACCESS_LOCAL_NETWORK lands.
    compileSdk = 36

    defaultConfig {
        applicationId = "com.sysadmindoc.phonefork.helper"
        minSdk = 30
        targetSdk = 36
        versionCode = 4
        versionName = "0.9.3-pre"

        // The helper has no launcher activity and remains host-driven.
        // The host (Windows) drives every code path via `adb shell content query/insert/update`
        // against the providers declared in AndroidManifest.xml.
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
        debug {
            applicationIdSuffix = ".debug"
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlin {
        compilerOptions {
            jvmTarget.set(JvmTarget.JVM_17)
        }
    }

    buildFeatures {
        buildConfig = true
    }
}

dependencies {
    testImplementation("junit:junit:4.13.2")
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.annotation:annotation:1.9.1")
}
