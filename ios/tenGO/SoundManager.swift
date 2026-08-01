//
//  SoundManager.swift
//  tenGO
//
//  Synthèse temps réel — Piano électrique doux (type Rhodes).
//
//  Architecture :
//   - 8 voix pré-allouées (AVAudioSourceNode), polyphonie sans allocation runtime
//   - Oscillateur par voix : mélange sinus 85% + triangle 15% (doux, peu d'harmoniques)
//   - Enveloppe ADSR (0.05s / 0.2s / 30% / 2.0s) appliquée dans le render block
//   - Arpégiateur : file FIFO dépilée toutes les 150 ms → pas d'empilement brutal
//   - Chaîne d'effets : Voix → EQ passe-bas 1000 Hz → Reverb largeRoom 30% → Sortie
//   - Sortie globale atténuée à 50 % pour un rendu feutré, non agressif
//
//  Règle d'or : aucune allocation dans les render blocks.
//

import AVFoundation
import Foundation
import QuartzCore

final class SoundManager {
    static let shared = SoundManager()

    // MARK: - Moteur audio et chaîne d'effets

    private let engine = AVAudioEngine()
    private let mixer  = AVAudioMixerNode()
    private let eq     = AVAudioUnitEQ(numberOfBands: 1)
    private let reverb = AVAudioUnitReverb()
    private let sampleRate: Double = 44100

    // MARK: - Pool de voix

    private var voices: [Voice] = []
    private static let voiceCount = 8

    // MARK: - Arpégiateur

    private struct QueuedNote {
        let frequency: Double
        let haptic: Bool   // true → vibration synchronisée sur cette note
        var velocity: Float = 1.0
    }

    /// Anti-saturation des 8 voix sur un swipe très rapide.
    private var lastImmediateAt: TimeInterval = 0
    private static let immediateThrottle: TimeInterval = 0.040

    private var noteQueue: [QueuedNote] = []
    private let queueLock = NSLock()
    private var arpTimer: DispatchSourceTimer?
    private static let arpInterval: TimeInterval = 0.150

    // MARK: - Sélection dynamique de gamme

    private var currentScale: [Double] = []
    /// Mémorise les notes réellement jouées pendant le chemin (pour le combo)
    private var pathNotes: [Double] = []
    private var lastScaleKey: Int = -1

    private let scales: [[Int]] = [
        [0, 2, 4, 7, 9],    // Pentatonique majeure
        [0, 3, 5, 7, 10],   // Pentatonique mineure
        [0, 2, 3, 7, 8],    // Hirajoshi
        [0, 2, 3, 7, 9],    // Kumoi
        [0, 1, 5, 7, 10],   // In Sen
        [0, 2, 4, 7, 11]    // Hemitonic pentatonic
    ]

    private let roots: [Double] = [
        196.0, 220.0, 233.1, 246.9, 261.6, 277.2, 293.7, 311.1, 329.6
    ]

    // MARK: - État

    var isMuted: Bool {
        get { UserDefaults.standard.bool(forKey: "tenGO_soundMuted") }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_soundMuted") }
    }

    // MARK: - Init

    private init() {
        configureSession()
        setupVoices()
        buildGraph()
        startArpeggiator()
        observeInterruptions()
        pickNewScale()  // gamme initiale (renouvelée après chaque combo)
    }

    // MARK: - Interface publique

    /// Première bulle — la valeur détermine la note dans la gamme courante
    /// (même bulle = même note, tant que la gamme n'a pas été renouvelée par un combo)
    ///
    /// Joué HORS FIFO : la note du tracé doit sortir sous le doigt. Via
    /// l'arpégiateur, un chemin de 5 bulles finissait sa mélodie 750 ms après
    /// le geste. La FIFO reste utilisée pour les mélodies (combo, victoire),
    /// où l'égrenage régulier est justement l'effet recherché.
    func playSelect(bubbleValue: Int) {
        guard !isMuted else { return }
        let idx = noteIndex(for: bubbleValue)
        let freq = currentScale[idx]
        pathNotes = [freq]
        playImmediate(frequency: freq, velocity: 0.7)
    }

    /// Bulle suivante — même logique que playSelect.
    /// `tension` ∈ [0,1] (somme courante / 10) nuance le volume, et les deux
    /// dernières unités montent d'une octave : la progression s'ENTEND sans
    /// qu'aucun chiffre ne soit affiché.
    func playConnect(bubbleValue: Int, tension: Double = 0) {
        guard !isMuted else { return }
        let idx = noteIndex(for: bubbleValue)
        var freq = currentScale[idx]
        // La mélodie mémorisée reste celle de la gamme : le replay du combo
        // doit rester exactement ce que le joueur a tracé.
        pathNotes.append(freq)
        if tension >= 0.8 { freq *= 2 }
        playImmediate(frequency: freq, velocity: Float(0.7 + 0.3 * min(1, max(0, tension))))
    }

    /// Bulle refusée (elle ferait dépasser 10) — note grave et discrète.
    func playRejected() {
        guard !isMuted else { return }
        playImmediate(frequency: 110.0, velocity: 0.30)
    }

    func playBacktrack() {
        if !pathNotes.isEmpty { pathNotes.removeLast() }
    }

    /// Combo — rejoue exactement la mélodie tracée par le joueur + quinte finale
    /// (SANS haptic sur chaque note : le medium de GameScene marque déjà la validation)
    /// Renouvelle la gamme après validation → nouvelle ambiance pour le combo suivant.
    /// `length` = longueur de la chaîne validée : la résolution s'étoffe avec
    /// elle (quinte → + octave → + accord), sans jamais altérer le replay de
    /// la mélodie tracée, qui doit rester exactement ce que le joueur a joué.
    func playCombo(length: Int = 0) {
        guard !isMuted else { return }
        let melody = pathNotes
        queueLock.lock()
        for freq in melody {
            noteQueue.append(QueuedNote(frequency: freq, haptic: false))
        }
        if let last = melody.last {
            noteQueue.append(QueuedNote(frequency: last * 1.498, haptic: false))
            if length >= 4 {
                noteQueue.append(QueuedNote(frequency: last * 2, haptic: false))
            }
        }
        queueLock.unlock()

        // Grandes chaînes : accord final, posé après l'égrenage de la mélodie.
        if length >= 6, let last = melody.last {
            let delay = Double(melody.count + 2) * SoundManager.arpInterval
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                self?.playChord([last, last * 1.498, last * 2], velocity: 0.5)
            }
        }

        pathNotes = []
        pickNewScale()  // nouvelle gamme pour la prochaine tentative
    }

    func cancelPath() { pathNotes = [] }

    /// Mappe la valeur d'une bulle (1–9) sur un index sûr dans `currentScale`
    private func noteIndex(for bubbleValue: Int) -> Int {
        let raw = bubbleValue - 1
        return max(0, min(raw, currentScale.count - 1))
    }

    /// Victoire — mélodie ascendante générée dans la gamme courante.
    /// 6 à 8 notes, sauts +1/+2 dans `currentScale`, terminée sur la note la plus aiguë.
    func playWin() {
        guard !isMuted else { return }
        guard currentScale.count >= 2 else { return }

        let noteCount = Int.random(in: 10...12)
        let topIndex = currentScale.count - 1
        // On réserve l'index du haut pour la note finale : intermédiaires clampés en-dessous
        let intermediateCeiling = topIndex - 1

        // Première note : parmi les graves (3 premiers index, borné sur la taille)
        var index = Int.random(in: 0...min(2, intermediateCeiling))

        queueLock.lock()

        // Première note
        noteQueue.append(QueuedNote(frequency: currentScale[index], haptic: false))

        // Notes intermédiaires — sauts +1 ou +2 vers les aigus
        let intermediateCount = noteCount - 2
        for _ in 0..<intermediateCount {
            let jump = Int.random(in: 1...2)
            index = min(index + jump, intermediateCeiling)
            noteQueue.append(QueuedNote(frequency: currentScale[index], haptic: false))
        }

        // Résolution : la plus aiguë de la gamme
        noteQueue.append(QueuedNote(frequency: currentScale[topIndex], haptic: false))

        queueLock.unlock()

        // Accord parfait final, une fois la mélodie égrenée.
        let root = currentScale[0]
        let delay = Double(noteCount + 1) * SoundManager.arpInterval
        DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
            self?.playChord([root, root * 1.498, root * 2, root * 2.52], velocity: 0.45)
        }
    }

    func playLose() {
        guard !isMuted else { return }
        scheduleNote(frequency: 98.0)
    }

    // MARK: - Queue FIFO

    private func scheduleNote(frequency: Double, haptic: Bool = true) {
        queueLock.lock()
        noteQueue.append(QueuedNote(frequency: frequency, haptic: haptic))
        queueLock.unlock()
    }

    // MARK: - Arpégiateur (tick 150 ms)

    private func startArpeggiator() {
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + SoundManager.arpInterval,
                       repeating: SoundManager.arpInterval,
                       leeway: .milliseconds(10))
        timer.setEventHandler { [weak self] in self?.arpeggiatorTick() }
        timer.resume()
        self.arpTimer = timer
    }

    private func arpeggiatorTick() {
        queueLock.lock()
        guard !noteQueue.isEmpty else {
            queueLock.unlock()
            return
        }
        let note = noteQueue.removeFirst()
        queueLock.unlock()
        triggerVoice(frequency: note.frequency, haptic: note.haptic)
    }

    // MARK: - Allocation de voix (libre ou vol de voix)

    private func triggerVoice(frequency: Double, haptic: Bool, velocity: Float = 1.0) {
        // Vibration synchronisée avec la note, pour les mélodies jouées à la
        // file. Le tracé, lui, déclenche son haptique directement depuis la
        // scène : il doit répondre sous le doigt et survivre au son coupé.
        if haptic { HapticManager.light() }

        if let freeVoice = voices.first(where: { !$0.isActive }) {
            freeVoice.noteOn(frequency: frequency, velocity: velocity)
            return
        }
        if let oldest = voices.min(by: { $0.startTime < $1.startTime }) {
            oldest.noteOn(frequency: frequency, velocity: velocity)
        }
    }

    /// Déclenche une voix sans passer par l'arpégiateur (latence nulle).
    private func playImmediate(frequency: Double, velocity: Float) {
        let now = CACurrentMediaTime()
        guard now - lastImmediateAt >= Self.immediateThrottle else { return }
        lastImmediateAt = now
        triggerVoice(frequency: frequency, haptic: false, velocity: velocity)
    }

    /// Notes simultanées (résolution des grandes chaînes, victoire).
    /// Hors FIFO et hors throttle : c'est justement la simultanéité qu'on veut.
    func playChord(_ frequencies: [Double], velocity: Float) {
        guard !isMuted else { return }
        for freq in frequencies {
            triggerVoice(frequency: freq, haptic: false, velocity: velocity)
        }
    }

    // MARK: - Sélection aléatoire de gamme

    private func pickNewScale() {
        var key: Int
        repeat {
            let r = Int.random(in: 0..<roots.count)
            let s = Int.random(in: 0..<scales.count)
            key = r * 100 + s
        } while key == lastScaleKey
        lastScaleKey = key

        let root = roots[key / 100]
        let offsets = scales[key % 100]

        var notes: [Double] = []
        notes.reserveCapacity(offsets.count * 2)
        for octave in 0..<2 {
            for offset in offsets {
                let semitones = Double(octave * 12 + offset)
                notes.append(root * pow(2.0, semitones / 12.0))
            }
        }
        currentScale = notes
    }

    // MARK: - Configuration moteur + session

    private func setupVoices() {
        voices.reserveCapacity(SoundManager.voiceCount)
        for _ in 0..<SoundManager.voiceCount {
            voices.append(Voice(sampleRate: sampleRate))
        }
    }

    private func buildGraph() {
        if let band = eq.bands.first {
            band.filterType = .lowPass
            band.frequency = 1000
            band.bypass = false
        }

        reverb.loadFactoryPreset(.largeRoom)
        reverb.wetDryMix = 30

        mixer.outputVolume = 0.5

        engine.attach(mixer)
        engine.attach(eq)
        engine.attach(reverb)

        for voice in voices {
            engine.attach(voice.sourceNode)
            engine.connect(voice.sourceNode, to: mixer, format: voice.format)
        }

        engine.connect(mixer,  to: eq,                   format: nil)
        engine.connect(eq,     to: reverb,               format: nil)
        engine.connect(reverb, to: engine.mainMixerNode, format: nil)

        do { try engine.start() }
        catch { print("[SoundManager] engine start : \(error)") }
    }

    private func configureSession() {
        do {
            let s = AVAudioSession.sharedInstance()
            try s.setCategory(.ambient, mode: .default, options: .mixWithOthers)
            try s.setActive(true)
        } catch { print("[SoundManager] session : \(error)") }
    }

    // MARK: - Interruptions audio

    private func observeInterruptions() {
        NotificationCenter.default.addObserver(
            self, selector: #selector(handleInterruption(_:)),
            name: AVAudioSession.interruptionNotification, object: nil)
        NotificationCenter.default.addObserver(
            self, selector: #selector(handleRouteChange(_:)),
            name: AVAudioSession.routeChangeNotification, object: nil)
    }

    @objc private func handleInterruption(_ notification: Notification) {
        guard let info = notification.userInfo,
              let typeValue = info[AVAudioSessionInterruptionTypeKey] as? UInt,
              let type = AVAudioSession.InterruptionType(rawValue: typeValue) else { return }
        switch type {
        case .began: engine.pause()
        case .ended:
            try? AVAudioSession.sharedInstance().setActive(true)
            do { try engine.start() }
            catch { print("[SoundManager] restart : \(error)") }
        @unknown default: break
        }
    }

    @objc private func handleRouteChange(_ notification: Notification) {
        if !engine.isRunning {
            do { try engine.start() }
            catch { print("[SoundManager] route restart : \(error)") }
        }
    }
}

// MARK: - Voice : une voix polyphonique (oscillateur + ADSR)

/// Chaque voix possède son propre `AVAudioSourceNode`.
/// L'état audio est modifié depuis le thread audio (render block).
/// `noteOn` est appelé depuis le thread principal ; les écritures sur
/// Double/Int/Bool sont atomiques sur iOS 64-bit → pas besoin de verrou.
private final class Voice {

    private enum Stage: Int { case idle, attack, decay, sustain, release }

    let format: AVAudioFormat

    /// Créé en `lazy` pour pouvoir capturer `self` dans le render block
    lazy var sourceNode: AVAudioSourceNode = {
        AVAudioSourceNode(format: format) { [unowned self] _, _, frameCount, ablPointer -> OSStatus in
            let abl = UnsafeMutableAudioBufferListPointer(ablPointer)
            self.render(frameCount: Int(frameCount), abl: abl)
            return noErr
        }
    }()

    // État audio-thread
    private var stage: Stage = .idle
    private var phase: Double = 0           // phase normalisée [0, 1)
    private var phaseIncrement: Double = 0  // freq / sampleRate
    private var sampleIndex: Int = 0
    // Niveau d'enveloppe courant : sert de point de départ à l'attaque suivante
    // lors d'un vol de voix → interpolation lisse, pas de clic.
    private var currentEnv: Float = 0
    private var attackStartEnv: Float = 0
    // Atténuation par voix : headroom pour éviter le clipping quand plusieurs
    // voix se superposent (cascades de combo, mélodie de victoire).
    private let voiceGain: Float = 0.6
    /// Nuance de la note en cours — permet à la tension du tracé de s'entendre.
    private var velocity: Float = 1.0

    // Flags lus depuis le thread principal
    private(set) var isActive: Bool = false
    private(set) var startTime: UInt64 = 0  // pour voice stealing

    // Constantes ADSR (en échantillons)
    private let sampleRate: Double
    private let attackSamples: Int
    private let decaySamples: Int
    private let sustainHoldSamples: Int
    private let releaseSamples: Int
    private let sustainLevel: Float = 0.3

    init(sampleRate: Double) {
        self.sampleRate = sampleRate
        self.attackSamples      = Int(0.05 * sampleRate)  // 0.05 s
        self.decaySamples       = Int(0.20 * sampleRate)  // 0.20 s
        self.sustainHoldSamples = Int(0.25 * sampleRate)  // maintien avant release auto
        self.releaseSamples     = Int(2.00 * sampleRate)  // 2.00 s

        guard let fmt = AVAudioFormat(standardFormatWithSampleRate: sampleRate, channels: 1) else {
            fatalError("Cannot create AVAudioFormat")
        }
        self.format = fmt
    }

    // MARK: - Déclenchement (thread principal)

    func noteOn(frequency: Double, velocity: Float = 1.0) {
        phaseIncrement = frequency / sampleRate
        // phase conservée → oscillateur continu sur vol de voix (anti-clic)
        attackStartEnv = currentEnv
        sampleIndex = 0
        stage = .attack
        startTime = DispatchTime.now().uptimeNanoseconds
        // Écriture d'un Float simple : atomique en pratique, et lue telle
        // quelle par le render block — pas d'allocation, règle d'or respectée.
        self.velocity = max(0, min(1, velocity))
        isActive = true
    }

    // MARK: - Render block (thread audio — AUCUNE allocation)

    private func render(frameCount: Int, abl: UnsafeMutableAudioBufferListPointer) {
        let bufferCount = abl.count
        for frame in 0..<frameCount {
            let sample = nextSample()
            for i in 0..<bufferCount {
                guard let ptr = abl[i].mData?.assumingMemoryBound(to: Float.self) else { continue }
                ptr[frame] = sample
            }
        }
    }

    /// Génère l'échantillon suivant — appelé ~44100 fois/seconde par voix active
    private func nextSample() -> Float {
        if stage == .idle { return 0 }

        // --- Enveloppe ADSR ---
        let env: Float
        switch stage {
        case .attack:
            // Interpolation depuis le niveau courant (0 pour une voix libre,
            // valeur en cours pour une voix volée) → transition continue.
            let progress = Float(sampleIndex) / Float(attackSamples)
            env = attackStartEnv + progress * (1.0 - attackStartEnv)
            sampleIndex += 1
            if sampleIndex >= attackSamples { stage = .decay; sampleIndex = 0 }

        case .decay:
            let progress = Float(sampleIndex) / Float(decaySamples)
            env = 1.0 - progress * (1.0 - sustainLevel)
            sampleIndex += 1
            if sampleIndex >= decaySamples { stage = .sustain; sampleIndex = 0 }

        case .sustain:
            env = sustainLevel
            sampleIndex += 1
            if sampleIndex >= sustainHoldSamples { stage = .release; sampleIndex = 0 }

        case .release:
            let progress = Float(sampleIndex) / Float(releaseSamples)
            env = sustainLevel * (1.0 - progress)
            sampleIndex += 1
            if sampleIndex >= releaseSamples {
                stage = .idle
                isActive = false
                currentEnv = 0
                return 0
            }

        case .idle:
            return 0
        }

        currentEnv = env

        // --- Oscillateur : 85% sinus + 15% triangle ---
        // Sinus : sin(2π · phase)
        let sineVal = Float(sin(phase * 2.0 * .pi))
        // Triangle direct depuis la phase [0, 1) : 4·|phase − 0.5| − 1 ∈ [-1, 1]
        let triangleVal = Float(4.0 * abs(phase - 0.5) - 1.0)
        let mixed: Float = sineVal * 0.85 + triangleVal * 0.15

        phase += phaseIncrement
        if phase >= 1.0 { phase -= 1.0 }

        return mixed * env * voiceGain * velocity
    }
}
