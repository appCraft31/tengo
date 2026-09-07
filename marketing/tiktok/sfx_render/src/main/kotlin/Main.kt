import com.tengo.audio.Lowpass
import com.tengo.audio.Reverb
import com.tengo.audio.Voice
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

/**
 * Rend la bande-son d'une publicité à partir du journal de notes de l'app.
 *
 * Le simulateur iOS n'enregistre pas l'audio et le jeu n'embarque aucun fichier
 * son : tout est synthétisé à l'exécution. Plutôt que de réinventer une
 * imitation, on relit le journal écrit par `SoundManager` (`SOUND_LOG=1`), qui
 * porte la partition exacte de la session — chaque note, son instant, sa
 * nuance — et on la rejoue ici avec le DSP du jeu lui-même (`Voice`, `Lowpass`,
 * `Reverb`, copiés sans modification depuis l'app Android).
 *
 * Usage :
 *   sfx-render <journal> <sortie.wav> [--start S] [--duration D] [--speed X]
 *              [--offset O] [--gain G]
 *
 *   --start     instant de la capture où commence l'extrait (s, horloge vidéo)
 *   --duration  durée de l'extrait AVANT accélération (s)
 *   --speed     accélération du montage : les notes sont rapprochées d'autant,
 *               ce qui évite tout `atempo` et garde des attaques nettes
 *   --offset    décalage de calage vidéo/son (s), positif = son plus tard
 *   --gain      gain maître (défaut 0.5, celui du jeu)
 */
private const val SAMPLE_RATE = 44100.0
private const val VOICE_COUNT = 8
private const val TAIL_SECONDS = 2.6   // queue de réverbération + release (2 s)

private data class Note(val time: Double, val frequency: Double, val velocity: Float)

fun main(args: Array<String>) {
    if (args.size < 2) {
        System.err.println("usage: sfx-render <journal> <sortie.wav> [--start S] [--duration D] [--speed X] [--offset O] [--gain G]")
        kotlin.system.exitProcess(2)
    }
    val logFile = File(args[0])
    val outFile = File(args[1])
    val opts = parseOptions(args.drop(2))

    val start = opts["start"] ?: 0.0
    val speed = opts["speed"] ?: 1.0
    val offset = opts["offset"] ?: 0.0
    val gain = (opts["gain"] ?: 0.5).toFloat()

    val notes = readNotes(logFile)
    if (notes.isEmpty()) {
        System.err.println("journal vide : ${logFile.path} (l'app a-t-elle tourné avec SOUND_LOG=1 ?)")
        kotlin.system.exitProcess(1)
    }

    // Le journal est horodaté sur l'horloge monotone de l'appareil ; `recordStart`
    // (écrit par le script de capture) donne l'instant zéro de la vidéo.
    val recordStart = readRecordStart(logFile) ?: notes.first().time
    val windowEnd = opts["duration"]?.let { start + it } ?: Double.MAX_VALUE

    // Recalage : temps vidéo, puis fenêtre de l'extrait, puis accélération.
    val timeline = notes.mapNotNull { n ->
        val videoTime = n.time - recordStart + offset
        if (videoTime < start || videoTime >= windowEnd) null
        else Note((videoTime - start) / speed, n.frequency, n.velocity)
    }
    if (timeline.isEmpty()) {
        System.err.println("aucune note dans la fenêtre demandée (start=$start durée=${opts["duration"]})")
        kotlin.system.exitProcess(1)
    }

    val lastNote = timeline.last().time
    val totalSeconds = (opts["duration"]?.let { it / speed } ?: lastNote) + TAIL_SECONDS
    val samples = render(timeline, totalSeconds, gain)
    writeWav(outFile, samples)

    val peak = samples.maxOf { kotlin.math.abs(it) }
    println("${timeline.size} notes · ${"%.2f".format(totalSeconds)} s · crête ${"%.2f".format(peak)} → ${outFile.path}")
    if (peak >= 0.999f) System.err.println("⚠ écrêtage : baisser --gain")
}

private fun parseOptions(args: List<String>): Map<String, Double> {
    val out = HashMap<String, Double>()
    var i = 0
    while (i < args.size) {
        val key = args[i].removePrefix("--")
        val value = args.getOrNull(i + 1)?.toDoubleOrNull()
        require(value != null) { "option sans valeur numérique : ${args[i]}" }
        out[key] = value
        i += 2
    }
    return out
}

/** `SFX NOTE <t> <hz> <vel>` — les autres lignes du journal sont ignorées. */
private fun readNotes(file: File): List<Note> =
    file.readLines().mapNotNull { line ->
        val parts = line.trim().split(Regex("\\s+"))
        if (parts.size >= 5 && parts[0] == "SFX" && parts[1] == "NOTE") {
            Note(parts[2].toDouble(), parts[3].toDouble(), parts[4].toFloat())
        } else null
    }

/**
 * `SFX RECORD_START <t>` est écrit par le script de capture au moment où
 * l'enregistrement vidéo démarre, sur la même horloge que les notes : c'est
 * l'origine des temps de la vidéo.
 */
private fun readRecordStart(file: File): Double? =
    file.readLines().firstNotNullOfOrNull { line ->
        val parts = line.trim().split(Regex("\\s+"))
        if (parts.size >= 3 && parts[0] == "SFX" && parts[1] == "RECORD_START") parts[2].toDoubleOrNull() else null
    }

/**
 * Rejoue la partition avec la chaîne du jeu : 8 voix, vol de la plus ancienne
 * quand elles sont toutes prises, passe-bas 1000 Hz, réverbération 30 %.
 */
private fun render(notes: List<Note>, seconds: Double, gain: Float): FloatArray {
    val total = (seconds * SAMPLE_RATE).roundToInt()
    val out = FloatArray(max(1, total))
    val voices = List(VOICE_COUNT) { Voice(SAMPLE_RATE) }
    val lowpass = Lowpass(1000.0, SAMPLE_RATE)
    val reverb = Reverb(0.30f)

    var next = 0
    for (i in out.indices) {
        // Déclenchement des notes dont l'instant est atteint.
        while (next < notes.size && notes[next].time * SAMPLE_RATE <= i) {
            val n = notes[next]
            val free = voices.firstOrNull { !it.isActive }
            val target = free ?: voices.minByOrNull { it.startTime }!!
            target.noteOn(n.frequency, i.toLong(), n.velocity)
            next++
        }
        var mix = 0f
        for (v in voices) mix += v.nextSample()
        mix = lowpass.process(mix)
        mix = reverb.process(mix)
        out[i] = (mix * gain).coerceIn(-1f, 1f)
    }
    return out
}

/** WAV PCM 16 bits stéréo (la même image sur les deux canaux). */
private fun writeWav(file: File, mono: FloatArray) {
    val channels = 2
    val bitsPerSample = 16
    val dataSize = mono.size * channels * bitsPerSample / 8
    val buffer = ByteBuffer.allocate(44 + dataSize).order(ByteOrder.LITTLE_ENDIAN)
    buffer.put("RIFF".toByteArray()).putInt(36 + dataSize).put("WAVE".toByteArray())
    buffer.put("fmt ".toByteArray()).putInt(16).putShort(1).putShort(channels.toShort())
    buffer.putInt(SAMPLE_RATE.toInt())
    buffer.putInt(SAMPLE_RATE.toInt() * channels * bitsPerSample / 8)
    buffer.putShort((channels * bitsPerSample / 8).toShort()).putShort(bitsPerSample.toShort())
    buffer.put("data".toByteArray()).putInt(dataSize)
    for (s in mono) {
        val v = (min(1f, max(-1f, s)) * 32767f).toInt().toShort()
        buffer.putShort(v); buffer.putShort(v)
    }
    file.parentFile?.mkdirs()
    file.writeBytes(buffer.array())
}
