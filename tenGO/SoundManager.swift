//
//  SoundManager.swift
//  tenGO
//
//  Synthèse organique v3 :
//  - Gouttes d'eau (chirp descendant) pour le lien entre bulles
//  - Kalimba FM pour la fusion (récompense chaude et boisée)
//  - Piano amélioré : extinction sélective des harmoniques + LP dynamique
//  - Tambour de bois grave pour l'échec (non-punitif)
//

import AVFoundation

final class SoundManager {
    static let shared = SoundManager()

    private let engine = AVAudioEngine()
    private let reverb  = AVAudioUnitReverb()
    private let sr      = 44100.0

    // Fréquences de la chaîne en cours (gamme pentatonique, position-based)
    private var pathFrequencies: [Double] = []

    // Pentatonique majeure ascendante — Do Ré Mi Sol La (×2 octaves)
    private let pentatonic: [Double] = [
        261.6, 293.7, 329.6, 392.0, 440.0,
        523.3, 587.3, 659.3, 784.0, 880.0
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

    /// Première bulle touchée — goutte d'eau (note 0 de la pentatonique)
    func playSelect(value: Int) {
        guard !isMuted else { return }
        let freq = pentatonic[0]
        pathFrequencies = [freq]
        waterDrop(freq: freq, amp: 0.30)
    }

    /// Bulle ajoutée — goutte suivante dans la gamme (ascendant)
    func playConnect(value: Int) {
        guard !isMuted else { return }
        let idx = min(pathFrequencies.count, pentatonic.count - 1)
        let freq = pentatonic[idx]
        pathFrequencies.append(freq)
        waterDrop(freq: freq, amp: 0.27)
    }

    /// Backtrack — goutte légèrement plus basse
    func playBacktrack() {
        guard !isMuted else { return }
        if !pathFrequencies.isEmpty { pathFrequencies.removeLast() }
        if let freq = pathFrequencies.last {
            waterDrop(freq: freq * 0.85, amp: 0.14)
        }
    }

    /// Combo réussi — strum kalimba de l'accord + pop de bulle
    func playCombo() {
        guard !isMuted else { return }
        let chord = pathFrequencies
        pathFrequencies = []
        for (i, freq) in chord.enumerated() {
            DispatchQueue.main.asyncAfter(deadline: .now() + Double(i) * 0.011) {
                self.kalimba(freq: freq, amp: min(0.32, 0.55 / Float(max(chord.count, 1))))
            }
        }
        // Quinte sur la dernière note → sensation de résolution harmonique
        if let last = chord.last {
            let delay = Double(chord.count) * 0.011
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.kalimba(freq: last * 1.498, amp: 0.16)
                self.bubblePop(amp: 0.10)
            }
        }
    }

    /// Annulation silencieuse
    func cancelPath() { pathFrequencies = [] }

    /// Victoire — arpège de piano ascendant
    func playWin() {
        guard !isMuted else { return }
        let notes: [(Double, Double)] = [
            (261.6, 0.00), (329.6, 0.18), (392.0, 0.36),
            (523.3, 0.56), (659.3, 0.78), (784.0, 1.02)
        ]
        for (freq, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.piano(freq: freq, amp: 0.24, duration: 1.4)
            }
        }
    }

    /// Défaite — tambour de bois grave, non-punitif
    func playLose() {
        guard !isMuted else { return }
        woodDrum(amp: 0.38)
    }

    // MARK: - Goutte d'eau (chirp descendant + réverb)

    private func waterDrop(freq: Double, amp: Float) {
        let dur = 0.24
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]
        let startFreq = freq * 2.0  // commence une octave au-dessus
        let k = 28.0                // vitesse de descente
        var phase = 0.0
        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            // Fréquence qui descend exponentiellement vers la cible
            let f_t = freq + (startFreq - freq) * exp(-t * k)
            phase += 2 * .pi * f_t / sr
            let env = Float(exp(-t * 15.0))
            data[i] = Float(sin(phase)) * amp * env
        }
        scheduleWet(buf)
    }

    // MARK: - Piano (win) : harmoniques sélectives + LP dynamique

    private func piano(freq: Double, amp: Float, duration: Double) {
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * duration)) else { return }
        let data = buf.floatChannelData![0]

        let attackTime = 0.008
        // Inharmonicité aléatoire — rien n'est jamais parfaitement fixe
        let r4 = 4.0 + Double.random(in: -0.015...0.015)
        let r5 = 5.0 + Double.random(in: -0.022...0.022)
        // Vibrato subtil (~corde qui vibre physiquement)
        let vRate = 2.3, vDepth = 0.0007

        var lpState: Float = 0
        // Filtre marteau HP (retire les basses du bruit)
        var hpPrev: Float = 0, hpOut: Float = 0

        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let u = max(0.0, t - attackTime)
            let attackFade: Float = t < attackTime ? Float(t / attackTime) : 1.0

            // Vibrato
            let f = freq * (1.0 + vDepth * sin(2 * .pi * vRate * t))

            // Extinction sélective — les hautes harmoniques s'éteignent bien plus vite
            let e1 = Float(exp(-u * 3.0))   // fondamentale : lente
            let e2 = Float(exp(-u * 4.8))   // octave
            let e3 = Float(exp(-u * 8.4))   // tierce
            let e4 = Float(exp(-u * 15.0))  // rapide
            let e5 = Float(exp(-u * 26.0))  // quasi instantanée

            let h1 = sin(Float(2 * .pi * f       * t)) * 0.55 * e1
            let h2 = sin(Float(2 * .pi * f * 2.0 * t)) * 0.22 * e2
            let h3 = sin(Float(2 * .pi * f * 3.0 * t)) * 0.13 * e3
            let h4 = sin(Float(2 * .pi * f * r4  * t)) * 0.07 * e4
            let h5 = sin(Float(2 * .pi * f * r5  * t)) * 0.03 * e5

            // Marteau boisé : bruit filtré bande 500–2000 Hz, < 6 ms
            var hammer: Float = 0
            if t < 0.006 {
                let fade = Float(1.0 - t / 0.006)
                let white = Float.random(in: -1...1)
                // HP one-pole (~500 Hz) : retire les basses
                let alphaHP: Float = 0.930
                hpOut = alphaHP * (hpOut + white - hpPrev)
                hpPrev = white
                hammer = hpOut * 0.13 * fade
            }

            let raw = (h1 + h2 + h3 + h4 + h5 + hammer) * attackFade

            // LP dynamique : brillant au début → sourd en fin de note
            let alphaLP = Float(0.12 + 0.82 * exp(-u * 6.0))
            lpState = lpState * (1 - alphaLP) + raw * alphaLP

            data[i] = lpState * amp
        }
        scheduleWet(buf)
    }

    // MARK: - Kalimba (FM synthesis) : tine chaleureuse et boisée

    private func kalimba(freq: Double, amp: Float) {
        let dur = 0.65
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]
        let modFreq = freq * 3.5
        let attackTime = 0.003

        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let u = max(0.0, t - attackTime)
            let attackFade: Float = t < attackTime ? Float(t / attackTime) : 1.0

            // Indice FM décroissant : commence métallique, devient pur et chaud
            let modIdx = Float(3.8 * exp(-u * 12.0))
            let mod = Float(sin(2 * .pi * modFreq * t))
            let carrier = sin(Float(2 * .pi * freq * t) + modIdx * mod)

            // Harmonique grave douce — chaleur de la caisse de résonance
            let h2 = sin(Float(2 * .pi * freq * 2.0 * t)) * 0.14 * Float(exp(-u * 14.0))

            // Enveloppe : attaque nette, résonance longue (tine métallique)
            let env = Float(exp(-u * 7.0) * 0.65 + exp(-u * 1.8) * 0.35)

            data[i] = (carrier + h2) * amp * env * attackFade
        }
        scheduleWet(buf)
    }

    // MARK: - Pop de bulle de savon

    private func bubblePop(amp: Float) {
        let dur = 0.07
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]
        var lp: Float = 0
        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let env = Float(exp(-t * 40.0))
            lp = lp * 0.82 + Float.random(in: -1...1) * 0.18
            data[i] = lp * amp * env
        }
        scheduleWet(buf)
    }

    // MARK: - Tambour de bois grave (défaite douce)

    private func woodDrum(amp: Float) {
        let dur = 0.45
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]
        var membrane: Float = 0
        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let env = Float(exp(-t * 11.0))
            // Corps de la frappe : sinusoïde grave (95 Hz)
            let body = sin(Float(2 * .pi * 95.0 * t)) * 0.70
            // Membrane : bruit rose très grave (passe-bas fort)
            let white = Float.random(in: -1...1)
            membrane = membrane * 0.975 + white * 0.025
            data[i] = (body + membrane * 0.30) * amp * env
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
        reverb.wetDryMix = 32
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
