plugins {
    alias(libs.plugins.android.library)
}

android {
    namespace = "com.example.pleosdemo.unitybridge"
    compileSdk {
        version = release(37)
    }
    useLibrary("android.car")

    defaultConfig {
        minSdk = 28
        consumerProguardFiles("consumer-rules.pro")
        // The library exposes JNI values; it has no resources requiring API 37.
        // Keep the packaging requirement separate from this module's compile SDK.
        aarMetadata { minCompileSdk = 32 }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}

dependencies {
    implementation("ai.pleos.playground:Vehicle:2.3.2")
    testImplementation(libs.junit)
}

tasks.register<Copy>("exportForUnity") {
    dependsOn("assembleDebug")
    from(layout.buildDirectory.file("outputs/aar/vehicle-bridge-debug.aar"))
    into(rootProject.file("unity/InputProbe/Assets/Plugins/Android"))
    rename { "pleos-vehicle-bridge.aar" }
}
