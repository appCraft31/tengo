//
//  SoundManager.swift
//  tenGO
//
//  Synthèse par modélisation physique (Karplus-Strong) :
//  simule une corde pincée en utilisant une ligne de retard avec
//  filtre passe-bas. Produit un timbre naturellement organique —
//  harpe / kora / kalimba — impossible à obtenir avec des sinus.
//

import AVFoundation

final class SoundManager {
    static let shared = SoundManager()

    private let engine = AVAudioEngine()
    private let reverb = AVAudioUnitReverb()
    private let sr     = 44100.0

    private var pathFrequencies: [Double] = []
    private var currentScale: [Double] = []

    // Demi-tons depuis la tonique, gammes zen (toutes harmonieusement stables)
    private let scales: [[Int]] = [
        [0, 2, 4, 7, 9],    // Pentatonique majeure — brillante
        [0, 3, 5, 7, 10],   // Pentatonique mineure — contemplative
        [0, 2, 3, 7, 8],    // Hirajoshi (Japon) — méditative
        [0, 2, 3, 7, 9],    // Kumoi (Japon) — sereine
        [0, 1, 5, 7, 10],   // In Sen (Japon) — mystérieuse
        [0, 2, 4, 7, 11]    // Hemitonic pentatonic — douce lumineuse
    ]

    // Toniques disponibles (Hz) — registre confortable, chaleur médium
    private let roots: [Double] = [
        196.0,  // G3
        220.0,  // A3
        233.1,  // A#3
        246.9,  // B3
        261.6,  // C4
        277.2,  // C#4
        293.7,  // D4
        311.1,  // D#4
        329.6   // E4
    ]

    var isMuted: Bool {
        get { UserDefaults.standard.bool(forKey: "tenGO_soundMuted") }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_soundMuted") }
    }

    private init() {
        configureSession()
        buildGraph()
    }

    // MARK: - Interface publique

    func playSelect(value: Int) {
        guard !isMuted else { return }
        pickNewScale()
        let freq = currentScale[0]
        pathFrequencies = [freq]
        pluck(freq: freq, amp: 0.38, duration: 0.9, damping: 0.994, brightness: 0.55)
    }

    func playConnect(value: Int) {
        guard !isMuted else { return }
        let idx = min(pathFrequencies.count, currentScale.count - 1)
        let freq = currentScale[idx]
        pathFrequencies.append(freq)
        pluck(freq: freq, amp: 0.35, duration: 0.8, damping: 0.993, brightness: 0.50)
    }

    func playBacktrack() {
        guard !isMuted else { return }
        if !pathFrequencies.isEmpty { pathFrequencies.removeLast() }
        if let freq = pathFrequencies.last {
            pluck(freq: freq * 0.85, amp: 0.18, duration: 0.5, damping: 0.988, brightness: 0.40)
        }
    }

    /// Combo — strum de toutes les notes avec quinte finale
    func playCombo() {
        guard !isMuted else { return }
        let chord = pathFrequencies
        pathFrequencies = []
        let baseAmp: Float = min(0.38, 0.58 / Float(max(chord.count, 1)))
        for (i, freq) in chord.enumerated() {
            let delay = Double(i) * 0.014
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.pluck(freq: freq, amp: baseAmp,
                           duration: 1.6, damping: 0.997, brightness: 0.60)
            }
        }
        // Quinte finale pour la résolution harmonique
        if let last = chord.last {
            let delay = Double(chord.count) * 0.014 + 0.010
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.pluck(freq: last * 1.498, amp: 0.22,
                           duration: 1.8, damping: 0.997, brightness: 0.55)
            }
        }
    }

    func cancelPath() { pathFrequencies = [] }

    // MARK: - Sélection dynamique de gamme

    private var lastScaleKey: Int = -1

    /// Tire une tonique et une gamme au hasard ; évite de rejouer la même
    private func pickNewScale() {
        var key: Int
        repeat {
            let r = Int.random(in: 0..<roots.count)
            let s = Int.random(in: 0..<scales.count)
            key = r * 100 + s
        } while key == lastScaleKey && roots.count * scales.count > 1
        lastScaleKey = key

        let root = roots[key / 100]
        let offsets = scales[key % 100]

        // Construit 2 octaves : donne 10 notes pour un chemin long
        var notes: [Double] = []
        for octave in 0..<2 {
            for offset in offsets {
                let semitones = Double(octave * 12 + offset)
                notes.append(root * pow(2.0, semitones / 12.0))
            }
        }
        currentScale = notes
    }

    func playWin() {
        guard !isMuted else { return }
        let notes: [(Double, Double)] = [
            (261.6, 0.00), (329.6, 0.18), (392.0, 0.36),
            (523.3, 0.56), (659.3, 0.78), (784.0, 1.02)
        ]
        for (freq, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.pluck(freq: freq, amp: 0.28,
                           duration: 1.8, damping: 0.996, brightness: 0.55)
            }
        }
    }

    func playLose() {
        guard !isMuted else { return }
        // Frappe de bois grave : Karplus-Strong à basse fréquence + forte friction
        pluck(freq: 98.0, amp: 0.45, duration: 0.55,
              damping: 0.975, brightness: 0.18)
    }

    // MARK: - Karplus-Strong : modélisation physique d'une corde

    /// Simule une corde pincée (ou une lame de kalimba).
    ///
    /// - freq       : hauteur cible (Hz)
    /// - amp        : amplitude de sortie
    /// - duration   : durée max du son (s)
    /// - damping    : facteur de feedback (0.99 court, 0.998 long sustain)
    /// - brightness : proportion de bruit vs impulse à l'excitation (0 = pur, 1 = bruit)
    private func pluck(freq: Double, amp: Float, duration: Double,
                       damping: Float, brightness: Float) {
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * duration)) else { return }
        let data = buf.floatChannelData![0]

        // Taille de la ligne de retard = période en échantillons
        let period = max(2, Int(sr / freq))
        var delayLine = [Float](repeating: 0, count: period)

        // Excitation : mélange d'impulse (frappe) et de bruit (brillance)
        // Le bruit rose crée le "bruit de pince" / de marteau naturellement
        var pink: Float = 0
        for i in 0..<period {
            let white = Float.random(in: -1...1)
            pink = pink * 0.96 + white * 0.04     // filtre rose one-pole
            // Fenêtre douce pour éviter les clics : attaque + decay sur la période
            let window = sin(Float.pi * Float(i) / Float(period - 1))
            let impulse: Float = i < period / 4 ? 0.8 : 0.0
            delayLine[i] = (pink * 12.0 * brightness + impulse * (1 - brightness)) * window
        }

        // Normalisation pour éviter la saturation initiale
        let peak = delayLine.map { abs($0) }.max() ?? 1
        if peak > 0 { for i in 0..<period { delayLine[i] /= peak } }

        // Variables du filtre LP one-pole pour amortissement dynamique
        var lpPrev: Float = 0
        var readIdx = 0

        // Le facteur d'amortissement évolue légèrement → corde qui "se détend"
        let baseDamping = damping
        let dampingVar: Float = 0.001

        for i in 0..<Int(buf.frameLength) {
            let t = Float(Double(i) / sr)
            let output = delayLine[readIdx]

            // Filtre moyenneur (atténue les hautes fréquences chaque cycle)
            let next = (readIdx + 1) % period
            let averaged = (delayLine[readIdx] + delayLine[next]) * 0.5

            // Amortissement variable (micro-fluctuation organique)
            let dynDamping = baseDamping - dampingVar * sin(2 * .pi * 0.7 * t)

            // Filtre passe-bas supplémentaire sur la sortie de la boucle → son devient plus doux
            lpPrev = lpPrev * 0.20 + averaged * 0.80
            delayLine[readIdx] = lpPrev * dynDamping

            readIdx = next
            data[i] = output * amp
        }

        scheduleWet(buf)
    }

    // MARK: - Infrastructure

    private func makeBuffer(frames: AVAudioFrameCount) -> AVAudioPCMBuffer? {
        let fmt = AVAudioFormat(standardFormatWithSampleRate: sr, channels: 1)!
        guard let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: frames) else { return nil }
        buf.frameLength = frames
        return buf
    }

    private func scheduleWet(_ buffer: AVAudioPCMBuffer) {
        let node = AVAudioPlayerNode()
        engine.attach(node)
        engine.connect(node, to: reverb, format: buffer.format)
        node.scheduleBuffer(buffer) { [weak self] in
            DispatchQueue.main.async { self?.engine.detach(node) }
        }
        node.play()
    }

    private func buildGraph() {
        reverb.loadFactoryPreset(.largeChamber)
        reverb.wetDryMix = 28
        engine.attach(reverb)
        engine.connect(reverb, to: engine.mainMixerNode, format: nil)
        do { try engine.start() }
        catch { print("[SoundManager] \(error)") }
    }

    private func configureSession() {
        do {
            let s = AVAudioSession.sharedInstance()
            try s.setCategory(.ambient, mode: .default, options: .mixWithOthers)
            try s.setActive(true)
        } catch { print("[SoundManager] Session : \(error)") }
    }
}
