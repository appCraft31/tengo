//
//  rng_vectors — vecteurs de référence pour la parité RNG iOS ↔ Android
//
//  Compile et exécute le CODE RÉEL du jeu (SeededGenerator, GridModel,
//  DailyChallenge, GridValidator) pour imprimer des valeurs que le portage
//  Android asserte dans `core-game/src/test/.../SwiftRngParityTest.kt` et
//  `DailyChallengeTest.kt`. Sans cette parité, un duel iOS ↔ Android donnerait
//  deux grilles différentes à partir de la même graine.
//
//  Usage :
//    swiftc -O -o /tmp/rng_vectors \
//      ios/tenGO/GridModel.swift ios/tenGO/BubbleModel.swift ios/tenGO/SeededGenerator.swift \
//      ios/tenGO/DailyChallenge.swift ios/tenGO/AppConfig.swift ios/tenGO/GameState.swift \
//      ios/tenGO/GridValidator.swift ios/tenGO/ScoreRules.swift ios/tools/rng_vectors/main.swift
//    TZ=Europe/Paris /tmp/rng_vectors
//
//  Le fuseau horaire fixe la clé du jour du Défi : le test Android utilise le
//  même (Europe/Paris) et la même date.
//

import Foundation

func line(_ label: String, _ values: [Int]) {
    print("\(label): " + values.map(String.init).joined(separator: ","))
}

var g = SeededGenerator(seed: 42)
var raw: [UInt64] = []
for _ in 0..<8 { raw.append(g.next()) }
print("raw: " + raw.map { String($0) }.joined(separator: ","))

g = SeededGenerator(seed: 42)
line("closed1to9", (0..<16).map { _ in Int.random(in: 1...9, using: &g) })

g = SeededGenerator(seed: 42)
line("half0to63", (0..<16).map { _ in Int.random(in: 0..<63, using: &g) })

g = SeededGenerator(seed: 42)
line("shuffle12", Array(0..<12).shuffled(using: &g))

g = SeededGenerator(seed: 42)
line("element7", (0..<8).map { _ in Array(0..<7).randomElement(using: &g)! })

g = SeededGenerator(seed: 42)
let grid = GridModel(using: &g)
var values: [Int] = []
for row in 0..<GridModel.rows {
    for col in 0..<GridModel.cols {
        values.append(grid.cells[row][col]?.value ?? 0)
    }
}
line("grid42", values)

// Grille du Défi pour une date fixe (midi, fuseau du process).
var calendar = Calendar(identifier: .gregorian)
calendar.timeZone = .current
let date = calendar.date(from: DateComponents(year: 2026, month: 9, day: 10, hour: 12))!
let today = DailyChallenge.make(for: date)
print("dailyKey: \(today.dayKey)")
print("dailyTwist: \(today.twist.rawValue)")
print("dailyDifficulty: \(today.difficulty)")
var cells: [String] = []
for row in 0..<GridModel.rows {
    for col in 0..<GridModel.cols {
        if today.grid.blocked[row][col] { cells.append("X"); continue }
        guard let b = today.grid.cells[row][col] else { cells.append("."); continue }
        var s = String(b.value)
        if b.isAnchored { s += "a" }
        if b.isFrozen { s += "f" }
        cells.append(s)
    }
}
print("dailyGrid: " + cells.joined(separator: ","))
