//
//  SoundManager.swift
//  tenGO
//
//  Synthèse organique : bol tibétain, carillon d'eau, gong.
//  Technique : attaque douce (12 ms), harmoniques naturelles (2x, 3x, 4x),
//  vibrato lent (4 Hz) et bruit de texture pour enlever le côté électronique.
//

import AVFoundation

final class SoundManager {
    static let shared = SoundManager()

    private let engine   = AVAudioEngine()
    private let reverb   = AVAudioUnitReverb()
    private let eq       = AVAudioUnitEQ(numberOfBands: 1)
    private var players  : [AVAudioPlayerNode] = []
    private let sr       = 44100.0

    // Gamme pentatonique majeure — chaleur et sérénité
    private let scale: [Double] = [261.6, 293.7, 329.6, 392.0, 440.0,
                                   523.3, 587.3, 659.3, 784.0, 880.0]

    var isMuted: Bool {
        get { UserDefaults.standard.bool(forKey: "tenGO_soundMuted") }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_soundMuted") }
    }

    private init() {
        configureSession()
        buildGraph()
    }

    // MARK: - Interface publique

    /// Toucher initial — goutte d'eau cristalline
    func playSelect() {
        bowl(freq: 659.3, dur: 1.2, amp: 0.14, attack: 0.008, decay: 4.5)
    }

    /// Chaque bulle ajoutée au chemin — carillon ascendant doux
    func playConnect(pathIndex: Int) {
        let f = scale[min(pathIndex, scale.count - 1)]
        bowl(freq: f, dur: 1.0, amp: 0.16, attack: 0.012, decay: 4.0)
    }

    /// Combo réussi — bol tibétain grave et chaud
    func playCombo() {
        bowl(freq: 174.6, dur: 3.5, amp: 0.30, attack: 0.025, decay: 1.2)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) {
            self.bowl(freq: 261.6, dur: 2.8, amp: 0.12, attack: 0.020, decay: 1.6)
        }
    }

    /// Chute des bulles — souffle imperceptible
    func playFall() {
        breathe(dur: 0.18, amp: 0.025)
    }

    /// Victoire — arpège temple bells, montée apaisante
    func playWin() {
        let notes: [(Double, Double)] = [
            (261.6, 0.00), (329.6, 0.25),
            (392.0, 0.50), (523.3, 0.78),
            (659.3, 1.10)
        ]
        for (f, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.bowl(freq: f, dur: 3.0, amp: 0.22, attack: 0.018, decay: 1.4)
            }
        }
    }

    /// Défaite — descente douce, pas dramatique
    func playLose() {
        let notes: [(Double, Double)] = [
            (329.6, 0.00), (261.6, 0.30), (196.0, 0.60)
        ]
        for (f, delay) in notes {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.bowl(freq: f, dur: 2.0, amp: 0.16, attack: 0.022, decay: 1.8)
            }
        }
    }

    // MARK: - Synthèse

    /// Bol / carillon : attaque douce + harmoniques naturelles + vibrato + texture
    private func bowl(freq: Double, dur: Double, amp: Float, attack: Double, decay: Double) {
        guard !isMuted else { return }
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]

        let vibratoRate = 3.8          // Hz — légèrement irrégulier = organique
        let vibratoDepth = 0.0018      // ±0.18% — quasi imperceptible mais vivant

        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr

            // Enveloppe : montée douce puis décroissance exponentielle
            let env: Float
            if t < attack {
                env = Float(t / attack)          // attaque linéaire
            } else {
                env = Float(exp(-(t - attack) * decay))
            }

            // Vibrato sinusoïdal lent
            let vib = 1.0 + vibratoDepth * sin(2 * .pi * vibratoRate * t)

            // Harmoniques naturelles (ratios 1:2:3:4) — timbre organique de bol
            let f = freq * vib
            let h1 = sin(Float(2 * .pi * f       * t))
            let h2 = sin(Float(2 * .pi * f * 2.0 * t))
            let h3 = sin(Float(2 * .pi * f * 3.0 * t))
            let h4 = sin(Float(2 * .pi * f * 4.0 * t))
            let wave = h1 * 0.60 + h2 * 0.22 + h3 * 0.11 + h4 * 0.07

            // Micro-bruit de texture (enlève le côté synthétique pur)
            let texture = Float.random(in: -0.008...0.008)

            data[i] = (wave + texture) * amp * env
        }
        play(buf)
    }

    /// Souffle : bruit rose filtré — chute de bulle
    private func breathe(dur: Double, amp: Float) {
        guard !isMuted else { return }
        guard let buf = makeBuffer(frames: AVAudioFrameCount(sr * dur)) else { return }
        let data = buf.floatChannelData![0]
        var prev: Float = 0
        for i in 0..<Int(buf.frameLength) {
            let t = Double(i) / sr
            let env = Float(exp(-t * 18.0))
            // Bruit rose (filtre passe-bas one-pole) — plus doux que le bruit blanc
            let white = Float.random(in: -1...1)
            prev = prev * 0.96 + white * 0.04
            data[i] = prev * amp * env
        }
        play(buf)
    }

    // MARK: - Infrastructure

    private func makeBuffer(frames: AVAudioFrameCount) -> AVAudioPCMBuffer? {
        let fmt = AVAudioFormat(standardFormatWithSampleRate: sr, channels: 1)!
        guard let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: frames) else { return nil }
        buf.frameLength = frames
        return buf
    }

    private func play(_ buffer: AVAudioPCMBuffer) {
        let node = AVAudioPlayerNode()
        engine.attach(node)
        engine.connect(node, to: reverb, format: buffer.format)
        players.append(node)
        node.scheduleBuffer(buffer) { [weak self] in
            DispatchQueue.main.async {
                self?.engine.detach(node)
                self?.players.removeAll { $0 === node }
            }
        }
        node.play()
    }

    private func buildGraph() {
        // EQ légère : couper les très hautes fréquences (>8 kHz) — plus doux
        if let band = eq.bands.first {
            band.filterType  = .lowPass
            band.frequency   = 8000
            band.bypass      = false
        }
        reverb.loadFactoryPreset(.largeChamber)
        reverb.wetDryMix = 38

        engine.attach(eq)
        engine.attach(reverb)
        engine.connect(reverb, to: eq, format: nil)
        engine.connect(eq, to: engine.mainMixerNode, format: nil)

        do { try engine.start() }
        catch { print("[SoundManager] Engine : \(error)") }
    }

    private func configureSession() {
        do {
            let s = AVAudioSession.sharedInstance()
            try s.setCategory(.ambient, mode: .default, options: .mixWithOthers)
            try s.setActive(true)
        } catch {
            print("[SoundManager] Session : \(error)")
        }
    }
}
