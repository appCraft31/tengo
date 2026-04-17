//
//  SoundManager.swift
//  tenGO
//
//  Piano synthétique + système d'harmonie :
//  - Chaque bulle touchée → note de piano (valeur 1–9 = note chromatique zen)
//  - Chemin en construction → accord qui se construit note par note
//  - Combo (éclatement) → accord complet joué ensemble
//

import AVFoundation

final class SoundManager {
    static let shared = SoundManager()

    private let engine  = AVAudioEngine()
    private let reverb  = AVAudioUnitReverb()
    private let sr      = 44100.0

    // Notes accumulées pendant le drag (fréquences Hz)
    private var pathFrequencies: [Double] = []

    // Mapping valeur bulle (1–9) → fréquence piano, gamme pentatonique majeure zen
    // Do–Ré–Mi–Sol–La sur deux octaves, centré dans le médium chaud du piano
    private let noteMap: [Int: Double] = [
        1: 261.6,  // C4
        2: 293.7,  // D4
        3: 329.6,  // E4
        4: 392.0,  // G4
        5: 440.0,  // A4
        6: 523.3,  // C5
        7: 587.3,  // D5
        8: 659.3,  // E5
        9: 784.0   // G5
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

    /// Première bulle touchée — une note de piano seule
    func playSelect(value: Int) {
        guard !isMuted else { return }
        let freq = noteMap[value] ?? 440.0
        pathFrequencies = [freq]
        piano(freq: freq, amp: 0.38, duration: 1.8)
    }

    /// Bulle ajoutée au chemin — joue la note + résonance douce des précédentes
    func playConnect(value: Int) {
        guard !isMuted else { return }
        let freq = noteMap[value] ?? 440.0
        pathFrequencies.append(freq)
        // Note principale
        piano(freq: freq, amp: 0.32, duration: 1.6)
        // Résonance douce de toutes les notes précédentes → l'accord se construit
        for f in pathFrequencies.dropLast() {
            piano(freq: f, amp: 0.10, duration: 1.2)
        }
    }

    /// Backtrack — retire la dernière note
    func playBacktrack() {
        guard !isMuted else { return }
        if !pathFrequencies.isEmpty { pathFrequencies.removeLast() }
        // Son court et doux pour signaler le recul
        if let freq = pathFrequencies.last {
            piano(freq: freq, amp: 0.14, duration: 0.8)
        }
    }

    /// Combo — joue l'accord entier simultanément puis réinitialise
    func playCombo() {
        guard !isMuted else { return }
        let chord = pathFrequencies
        pathFrequencies = []
        let baseAmp: Float = min(0.28, 0.50 / Float(max(chord.count, 1)))
        for (i, freq) in chord.enumerated() {
            // Léger échelonnement (strum) : 18 ms entre chaque note
            let delay = Double(i) * 0.018
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.piano(freq: freq, amp: baseAmp, duration: 2.5)
            }
        }
    }

    /// Annulation chemin — réinitialise sans son
    func cancelPath() {
        pathFrequencies = []
    }

    /// Victoire — arpège ascendant lumineux
    func playWin() {
        guard !isMuted else { return }
        let notes: [(Double, Double)] = [
            (261.6, 0.00), (329.6, 0.20), (392.0, 0.40),
            (523.3, 0.62), (659.3, 0.86), (784.0, 1.14)
        ]
        for (freq, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.piano(freq: freq, amp: 0.26, duration: 3.0)
            }
        }
    }

    /// Défaite — accord doux qui descend
    func playLose() {
        guard !isMuted else { return }
        let notes: [(Double, Double)] = [
            (392.0, 0.00), (329.6, 0.28), (261.6, 0.56)
        ]
        for (freq, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.piano(freq: freq, amp: 0.20, duration: 2.0)
            }
        }
    }

    /// Chute des bulles — percussif discret
    func playFall() {
        guard !isMuted else { return }
        plink(amp: 0.06)
    }

    // MARK: - Synthèse piano

    private func piano(freq: Double, amp: Float, duration: Double) {
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * duration)) else { return }
        let data = buf.floatChannelData![0]

        // Enveloppe piano : frappe percussive (10 ms) + décroissance rapide + queue lente
        let attackTime = 0.010
        let fastDecay  = 12.0   // décroissance rapide post-frappe
        let slowDecay  =  1.8   // queue de résonance longue

        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr

            // Enveloppe deux étages
            let env: Float
            if t < attackTime {
                env = Float(t / attackTime)
            } else {
                let u = t - attackTime
                let fast = exp(-u * fastDecay)
                let slow = exp(-u * slowDecay) * 0.28
                env = Float(fast + slow)
            }

            // Harmoniques piano (inharmonicité légère sur les partiels hauts)
            let h1 = sin(Float(2 * .pi * freq       * t)) * 0.55
            let h2 = sin(Float(2 * .pi * freq * 2.0 * t)) * 0.20
            let h3 = sin(Float(2 * .pi * freq * 3.0 * t)) * 0.12
            let h4 = sin(Float(2 * .pi * freq * 4.01 * t)) * 0.08  // légère inharmonicité
            let h5 = sin(Float(2 * .pi * freq * 5.02 * t)) * 0.05

            // Bruit de marteau : bref burst au début uniquement
            let hammer = t < 0.008 ? Float.random(in: -0.18...0.18) * Float(1 - t / 0.008) : 0

            data[i] = (h1 + h2 + h3 + h4 + h5 + hammer) * amp * env
        }
        schedule(buf)
    }

    /// Percussif discret pour la chute de bulle
    private func plink(amp: Float) {
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * 0.15)) else { return }
        let data = buf.floatChannelData![0]
        var lp: Float = 0
        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let env = Float(exp(-t * 25.0))
            let white = Float.random(in: -1...1)
            lp = lp * 0.92 + white * 0.08
            data[i] = lp * amp * env
        }
        schedule(buf)
    }

    // MARK: - Infrastructure

    private func makeBuffer(frames: AVAudioFrameCount) -> AVAudioPCMBuffer? {
        let fmt = AVAudioFormat(standardFormatWithSampleRate: sr, channels: 1)!
        guard let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: frames) else { return nil }
        buf.frameLength = frames
        return buf
    }

    private func schedule(_ buffer: AVAudioPCMBuffer) {
        let node = AVAudioPlayerNode()
        engine.attach(node)
        engine.connect(node, to: reverb, format: buffer.format)
        node.scheduleBuffer(buffer) { [weak self] in
            DispatchQueue.main.async {
                self?.engine.detach(node)
            }
        }
        node.play()
    }

    private func buildGraph() {
        reverb.loadFactoryPreset(.mediumHall)
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
