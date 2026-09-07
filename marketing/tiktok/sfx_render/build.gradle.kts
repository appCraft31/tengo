plugins {
    kotlin("jvm") version "2.2.10"
    application
}

repositories { mavenCentral() }

kotlin { jvmToolchain(17) }

application { mainClass.set("MainKt") }

// Le DSP du jeu est repris TEL QUEL depuis l'app Android : `Voice`, `Lowpass` et
// `Reverb` n'ont aucun import `android.*`. C'est ce qui garantit que la
// bande-son des publicités est le son du jeu, et non une imitation qui
// dériverait à la première retouche du synthé.
val gameAudioDir = file("../../../android/app/src/main/java/com/tengo/audio")
val importedAudio = layout.buildDirectory.dir("gameAudio")

val importGameAudio by tasks.registering(Copy::class) {
    doFirst {
        require(gameAudioDir.isDirectory) {
            "DSP du jeu introuvable : $gameAudioDir — android/ doit être à côté de marketing/."
        }
    }
    from(gameAudioDir) { include("Voice.kt", "Dsp.kt") }
    into(importedAudio.map { it.dir("com/tengo/audio") })
}

sourceSets["main"].kotlin.srcDir(importedAudio)
tasks.named("compileKotlin") { dependsOn(importGameAudio) }
