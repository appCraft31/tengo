//
//  SoundManager.swift
//  tenGO
//
//  Synthèse temps réel — Piano électrique doux (type Rhodes).
//
//  Le son est un retour d'information, pas une bande-son :
//   - chaque bulle joue sa note, dans une gamme FIXE (un 7 sonne toujours
//     pareil : l'oreille apprend la grille) ;
//   - à l'approche de 10, la note monte d'une octave : la progression
//     s'entend sans qu'aucun chiffre ne soit affiché ;
//   - une chaîne validée se résout par un accord bref, immédiat, qui
//     s'étoffe avec la longueur de la chaîne ;
//   - la victoire tient en trois notes montantes et un accord.
//
//  Architecture :
//   - 8 voix pré-allouées (AVAudioSourceNode), polyphonie sans allocation runtime
//   - Oscillateur par voix : mélange sinus 85% + triangle 15% (doux, peu d'harmoniques)
//   - Enveloppe ADSR appliquée dans le render block : courte et feutrée pour
//     les notes du tracé (≈ 0,5 s), longue pour les accords (≈ 2,5 s)
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

    // MARK: - Journal des notes (capture vidéo marketing)

    /// `SOUND_LOG=1` : chaque note déclenchée est imprimée avec son horodatage.
    /// La bande-son des publicités est rendue hors ligne à partir de ce journal
    /// (`marketing/tiktok/`), car le simulateur n'enregistre pas l'audio.
    /// Sans la variable d'environnement, pas une ligne n'est écrite.
    private let soundLogEnabled = ProcessInfo.processInfo.environment["SOUND_LOG"] == "1"
    private var didLogEpoch = false

    // MARK: - Pool de voix

    private var voices: [Voice] = []
    private static let voiceCount = 8

    /// Anti-saturation des 8 voix sur un swipe très rapide.
    private var lastImmediateAt: TimeInterval = 0
    private static let immediateThrottle: TimeInterval = 0.040

    /// Décalage entre les notes d'un accord « gratté » : assez court pour
    /// sonner comme un seul geste, assez long pour ne pas claquer.
    private static let strumInterval: TimeInterval = 0.025

    // MARK: - Gamme fixe

    /// Pentatonique majeure de La (220 Hz) sur deux octaves : 10 notes pour
    /// les valeurs 1 à 9. Aucune dissonance possible, quel que soit le tracé.
    private let scale: [Double] = {
        let root = 220.0
        let offsets = [0, 2, 4, 7, 9]
        return (0..<2).flatMap { octave in
            offsets.map { root * pow(2.0, Double(octave * 12 + $0) / 12.0) }
        }
    }()

    /// Notes du tracé en cours (dans la gamme, sans le saut d'octave) :
    /// la dernière sert de fondamentale à l'accord de résolution.
    private var pathNotes: [Double] = []

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
        observeInterruptions()
    }

    // MARK: - Interface publique

    /// Première bulle — la valeur détermine la note (toujours la même).
    /// Les notes du tracé sont courtes et feutrées : elles accompagnent le
    /// geste sans s'empiler (enveloppe courte, vélocité modérée).
    func playSelect(bubbleValue: Int) {
        guard !isMuted else { return }
        let freq = scale[noteIndex(for: bubbleValue)]
        pathNotes = [freq]
        playImmediate(frequency: freq, velocity: 0.45)
    }

    /// Bulle suivante — même logique que playSelect.
    /// `tension` ∈ [0,1] (somme courante / 10) nuance le volume, et les deux
    /// dernières unités montent d'une octave : la progression s'ENTEND sans
    /// qu'aucun chiffre ne soit affiché.
    func playConnect(bubbleValue: Int, tension: Double = 0) {
        guard !isMuted else { return }
        var freq = scale[noteIndex(for: bubbleValue)]
        pathNotes.append(freq)
        if tension >= 0.8 { freq *= 2 }
        playImmediate(frequency: freq, velocity: Float(0.45 + 0.2 * min(1, max(0, tension))))
    }

    /// Bulle refusée (elle ferait dépasser 10) — note grave et discrète.
    func playRejected() {
        guard !isMuted else { return }
        playImmediate(frequency: 110.0, velocity: 0.30)
    }

    func playBacktrack() {
        if !pathNotes.isEmpty { pathNotes.removeLast() }
    }

    /// Chaîne validée — accord bref et immédiat sur la dernière note tracée.
    /// Il s'étoffe avec la longueur : quinte, puis octave, puis tierce aiguë.
    /// (Pas d'haptique ici : le medium de GameScene marque déjà la validation.)
    func playCombo(length: Int = 0) {
        defer { pathNotes = [] }
        guard !isMuted else { return }
        let base = pathNotes.last ?? scale[0]
        var chord = [base, base * 1.498]
        if length >= 4 { chord.append(base * 2) }
        if length >= 6 { chord.append(base * 2.52) }
        strum(chord, velocity: 0.55)
    }

    func cancelPath() { pathNotes = [] }

    /// Mappe la valeur d'une bulle (1–9) sur un index sûr dans la gamme.
    private func noteIndex(for bubbleValue: Int) -> Int {
        max(0, min(bubbleValue - 1, scale.count - 1))
    }

    /// Victoire — trois notes montantes puis l'accord de la tonique (< 1 s).
    func playWin() {
        guard !isMuted else { return }
        let steps = [scale[4], scale[7], scale[9]]
        for (i, freq) in steps.enumerated() {
            after(Double(i) * 0.11) { $0.triggerVoice(frequency: freq, velocity: 0.8) }
        }
        let root = scale[5]
        after(0.40) { $0.playChord([root, root * 1.498, root * 2, root * 2.52], velocity: 0.45) }
    }

    /// Défaite — une note grave, avec une vibration légère.
    func playLose() {
        guard !isMuted else { return }
        HapticManager.light()
        triggerVoice(frequency: 98.0, velocity: 1.0)
    }

    /// Notes simultanées (résolution de la victoire).
    func playChord(_ frequencies: [Double], velocity: Float) {
        guard !isMuted else { return }
        for freq in frequencies {
            triggerVoice(frequency: freq, velocity: velocity)
        }
    }

    // MARK: - Déclenchement

    /// Accord légèrement égrené (quelques millisecondes entre les notes).
    private func strum(_ frequencies: [Double], velocity: Float) {
        for (i, freq) in frequencies.enumerated() {
            after(Double(i) * Self.strumInterval) { $0.triggerVoice(frequency: freq, velocity: velocity) }
        }
    }

    private func after(_ delay: TimeInterval, _ action: @escaping (SoundManager) -> Void) {
        guard delay > 0 else { action(self); return }
        DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
            guard let self, !self.isMuted else { return }
            action(self)
        }
    }

    /// Allocation de voix : libre en priorité, sinon vol de la plus ancienne.
    private func triggerVoice(frequency: Double, velocity: Float = 1.0, short: Bool = false) {
        // Un seul point de journalisation : toutes les voix passent ici, quelle
        // que soit la route (tracé, accord, victoire). Le journal porte donc la
        // partition exacte de la session, sans rien réinventer.
        if soundLogEnabled {
            logNote(frequency: frequency, velocity: velocity)
        }

        if let freeVoice = voices.first(where: { !$0.isActive }) {
            freeVoice.noteOn(frequency: frequency, velocity: velocity, short: short)
            return
        }
        if let oldest = voices.min(by: { $0.startTime < $1.startTime }) {
            oldest.noteOn(frequency: frequency, velocity: velocity, short: short)
        }
    }

    /// Écrit une note sur la sortie standard : `NOTE <t> <hz> <vel>`.
    /// `t` est l'horloge monotone du système ; la première ligne (`EPOCH`) la
    /// rattache à l'heure murale, seule référence commune avec le script qui
    /// lance l'enregistrement vidéo.
    private func logNote(frequency: Double, velocity: Float) {
        let now = CACurrentMediaTime()
        if !didLogEpoch {
            didLogEpoch = true
            print(String(format: "SFX EPOCH %.6f %.6f", Date().timeIntervalSince1970, now))
        }
        print(String(format: "SFX NOTE %.6f %.4f %.4f", now, frequency, velocity))
    }

    /// Déclenche une note courte immédiatement, sous le doigt (latence nulle).
    private func playImmediate(frequency: Double, velocity: Float) {
        let now = CACurrentMediaTime()
        guard now - lastImmediateAt >= Self.immediateThrottle else { return }
        lastImmediateAt = now
        triggerVoice(frequency: frequency, velocity: velocity, short: true)
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

    // Enveloppe ADSR (en échantillons). Deux profils :
    //  - long  (accords de combo, victoire) : 0.05 / 0.20 / maintien 0.25 / 2.00 s ;
    //  - court (notes du tracé) : 0.03 / 0.10 / maintien 0.03 / 0.35 s — une note
    //    douce qui s'éteint vite, pour ne pas s'empiler pendant un swipe.
    // Réglés sur le thread principal au noteOn, lus tels quels par le render block.
    private let sampleRate: Double
    private let longProfile: (attack: Int, decay: Int, hold: Int, release: Int)
    private let shortProfile: (attack: Int, decay: Int, hold: Int, release: Int)
    private var attackSamples: Int
    private var decaySamples: Int
    private var sustainHoldSamples: Int
    private var releaseSamples: Int
    private let sustainLevel: Float = 0.3

    init(sampleRate: Double) {
        self.sampleRate = sampleRate
        longProfile  = (Int(0.05 * sampleRate), Int(0.20 * sampleRate),
                        Int(0.25 * sampleRate), Int(2.00 * sampleRate))
        shortProfile = (Int(0.03 * sampleRate), Int(0.10 * sampleRate),
                        Int(0.03 * sampleRate), Int(0.35 * sampleRate))
        (attackSamples, decaySamples, sustainHoldSamples, releaseSamples) = longProfile

        guard let fmt = AVAudioFormat(standardFormatWithSampleRate: sampleRate, channels: 1) else {
            fatalError("Cannot create AVAudioFormat")
        }
        self.format = fmt
    }

    // MARK: - Déclenchement (thread principal)

    func noteOn(frequency: Double, velocity: Float = 1.0, short: Bool = false) {
        (attackSamples, decaySamples, sustainHoldSamples, releaseSamples) = short ? shortProfile : longProfile
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
