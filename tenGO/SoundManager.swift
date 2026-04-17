//
//  SoundManager.swift
//  tenGO
//
//  Sons synthétisés programmatiquement — thème zen :
//  bols tibétains, carillons, gamme pentatonique mineure.
//

import AVFoundation

final class SoundManager {
    static let shared = SoundManager()

    private let engine = AVAudioEngine()
    private let reverb = AVAudioUnitReverb()
    private var activePlayers: [AVAudioPlayerNode] = []
    private let sampleRate = 44100.0

    // Gamme pentatonique mineure — sonnorité zen
    private let pentatonic: [Double] = [220, 261.6, 293.7, 349.2, 392.0,
                                        440.0, 523.3, 587.3, 698.5, 784.0]

    var isMuted: Bool {
        get { UserDefaults.standard.bool(forKey: "tenGO_soundMuted") }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_soundMuted") }
    }

    private init() {
        setupAudioSession()
        reverb.loadFactoryPreset(.mediumHall2)
        reverb.wetDryMix = 45
        engine.attach(reverb)
        engine.connect(reverb, to: engine.mainMixerNode, format: nil)
        do {
            try engine.start()
        } catch {
            print("[SoundManager] Erreur démarrage engine : \(error)")
        }
    }

    private func setupAudioSession() {
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.ambient, mode: .default, options: .mixWithOthers)
            try session.setActive(true)
        } catch {
            print("[SoundManager] Erreur AVAudioSession : \(error)")
        }
    }

    // MARK: - Interface publique

    /// Première bulle touchée
    func playSelect() {
        playBell(frequency: 784.0, duration: 0.6, amplitude: 0.18, decay: 6.0)
    }

    /// Bulle ajoutée au chemin (index = position dans le chemin, 0-based)
    func playConnect(pathIndex: Int) {
        let freq = pentatonic[min(pathIndex, pentatonic.count - 1)]
        playBell(frequency: freq, duration: 0.5, amplitude: 0.22, decay: 5.0)
    }

    /// Combo réussi (somme = 10)
    func playCombo() {
        // Bol tibétain : fondamentale + harmonique légèrement désaccordée
        playBell(frequency: 392.0, duration: 2.0, amplitude: 0.32, decay: 2.2)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.08) {
            self.playBell(frequency: 528.0, duration: 1.6, amplitude: 0.12, decay: 2.8)
        }
    }

    /// Chute des bulles
    func playFall() {
        playNoise(duration: 0.12, amplitude: 0.04)
    }

    /// Victoire — arpège ascendant pentatonique
    func playWin() {
        let arpeggio: [(Double, Double)] = [
            (293.7, 0.0),
            (392.0, 0.18),
            (523.3, 0.36),
            (698.5, 0.56),
            (784.0, 0.80)
        ]
        for (freq, delay) in arpeggio {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.playBell(frequency: freq, duration: 2.5, amplitude: 0.28, decay: 1.8)
            }
        }
    }

    /// Défaite — descente douce
    func playLose() {
        let descent: [(Double, Double)] = [
            (440.0, 0.0),
            (349.2, 0.22),
            (261.6, 0.44)
        ]
        for (freq, delay) in descent {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                self.playBell(frequency: freq, duration: 1.2, amplitude: 0.20, decay: 3.0)
            }
        }
    }

    // MARK: - Synthèse

    private func playBell(frequency: Double, duration: Double, amplitude: Float, decay: Double) {
        guard !isMuted else { return }
        guard let buffer = makeBuffer(frameCount: AVAudioFrameCount(sampleRate * duration)) else { return }
        let data = buffer.floatChannelData![0]
        for i in 0..<Int(buffer.frameLength) {
            let t = Double(i) / sampleRate
            let env = Float(exp(-t * decay))
            // Fondamentale + partiel inharmonique → timbre de carillon / bol
            let wave = sin(Float(2 * .pi * frequency * t)) * 0.70
                     + sin(Float(2 * .pi * frequency * 2.756 * t)) * 0.20
                     + sin(Float(2 * .pi * frequency * 0.5 * t)) * 0.10
            data[i] = wave * amplitude * env
        }
        schedule(buffer)
    }

    private func playNoise(duration: Double, amplitude: Float) {
        guard !isMuted else { return }
        guard let buffer = makeBuffer(frameCount: AVAudioFrameCount(sampleRate * duration)) else { return }
        let data = buffer.floatChannelData![0]
        for i in 0..<Int(buffer.frameLength) {
            let t = Double(i) / sampleRate
            let env = Float(exp(-t * 20.0))
            data[i] = Float.random(in: -1...1) * amplitude * env
        }
        schedule(buffer)
    }

    private func makeBuffer(frameCount: AVAudioFrameCount) -> AVAudioPCMBuffer? {
        let format = AVAudioFormat(standardFormatWithSampleRate: sampleRate, channels: 1)!
        guard let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: frameCount) else { return nil }
        buffer.frameLength = frameCount
        return buffer
    }

    private func schedule(_ buffer: AVAudioPCMBuffer) {
        let player = AVAudioPlayerNode()
        engine.attach(player)
        engine.connect(player, to: reverb, format: buffer.format)
        activePlayers.append(player)

        player.scheduleBuffer(buffer) { [weak self] in
            DispatchQueue.main.async {
                self?.engine.detach(player)
                self?.activePlayers.removeAll { $0 === player }
            }
        }
        player.play()
    }
}
