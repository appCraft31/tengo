//
//  SoundManager.swift
//  tenGO
//
//  Synthèse temps réel — Piano électrique doux (type Rhodes).
//
//  Architecture :
//   - 8 voix pré-allouées (AVAudioSourceNode), polyphonie sans allocation runtime
//   - Oscillateur par voix : mélange sinus 70% + triangle 30%
//   - Enveloppe ADSR (0.05s / 0.2s / 30% / 2.0s) appliquée dans le render block
//   - Arpégiateur : file FIFO dépilée toutes les 150 ms → pas d'empilement brutal
//   - Chaîne d'effets : Voix → EQ passe-bas 1500 Hz → Reverb largeRoom 30% → Sortie
//
//  Règle d'or : aucune allocation dans les render blocks.
//

import AVFoundation
import Foundation

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

    private var noteQueue: [Double] = []
    private let queueLock = NSLock()
    private var arpTimer: DispatchSourceTimer?
    private static let arpInterval: TimeInterval = 0.150

    // MARK: - Sélection dynamique de gamme

    private var currentScale: [Double] = []
    private var pathStep: Int = 0
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
    }

    // MARK: - Interface publique

    func playSelect(value: Int) {
        guard !isMuted else { return }
        pickNewScale()
        pathStep = 0
        scheduleNote(frequency: currentScale[0])
    }

    func playConnect(value: Int) {
        guard !isMuted else { return }
        pathStep += 1
        let idx = min(pathStep, currentScale.count - 1)
        scheduleNote(frequency: currentScale[idx])
    }

    func playBacktrack() {
        pathStep = max(0, pathStep - 1)
    }

    /// Combo — enfile l'accord + quinte finale dans l'arpégiateur
    func playCombo() {
        guard !isMuted else { return }
        let count = min(pathStep + 1, currentScale.count)
        queueLock.lock()
        for i in 0..<count {
            noteQueue.append(currentScale[i])
        }
        if count > 0 {
            noteQueue.append(currentScale[count - 1] * 1.498)  // quinte
        }
        queueLock.unlock()
        pathStep = 0
    }

    func cancelPath() { pathStep = 0 }

    func playWin() {
        guard !isMuted else { return }
        let notes: [Double] = [261.6, 329.6, 392.0, 523.3, 659.3, 784.0]
        queueLock.lock()
        noteQueue.append(contentsOf: notes)
        queueLock.unlock()
    }

    func playLose() {
        guard !isMuted else { return }
        scheduleNote(frequency: 98.0)
    }

    // MARK: - Queue FIFO

    private func scheduleNote(frequency: Double) {
        queueLock.lock()
        noteQueue.append(frequency)
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
        let freq = noteQueue.removeFirst()
        queueLock.unlock()
        triggerVoice(frequency: freq)
    }

    // MARK: - Allocation de voix (libre ou vol de voix)

    private func triggerVoice(frequency: Double) {
        if let freeVoice = voices.first(where: { !$0.isActive }) {
            freeVoice.noteOn(frequency: frequency)
            return
        }
        if let oldest = voices.min(by: { $0.startTime < $1.startTime }) {
            oldest.noteOn(frequency: frequency)
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
            band.frequency = 1500
            band.bypass = false
        }

        reverb.loadFactoryPreset(.largeRoom)
        reverb.wetDryMix = 30

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

    func noteOn(frequency: Double) {
        phaseIncrement = frequency / sampleRate
        phase = 0
        sampleIndex = 0
        stage = .attack
        startTime = DispatchTime.now().uptimeNanoseconds
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
            env = Float(sampleIndex) / Float(attackSamples)
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
                return 0
            }

        case .idle:
            return 0
        }

        // --- Oscillateur : 70% sinus + 30% triangle ---
        // Sinus : sin(2π · phase)
        let sineVal = Float(sin(phase * 2.0 * .pi))
        // Triangle direct depuis la phase [0, 1) : 4·|phase − 0.5| − 1 ∈ [-1, 1]
        let triangleVal = Float(4.0 * abs(phase - 0.5) - 1.0)
        let mixed: Float = sineVal * 0.7 + triangleVal * 0.3

        phase += phaseIncrement
        if phase >= 1.0 { phase -= 1.0 }

        return mixed * env
    }
}
