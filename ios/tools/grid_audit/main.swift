//
//  grid_audit — audit des grilles du Défi du jour (issue #21)
//
//  Compile et exécute le CODE RÉEL du jeu (GridModel, DailyChallenge,
//  GridValidator), pas une copie : une divergence entre l'outil et le jeu est
//  donc impossible par construction.
//
//  Usage :
//    swiftc -O -o /tmp/grid_audit \
//      ios/tenGO/{GridModel,BubbleModel,SeededGenerator,DailyChallenge,AppConfig,GameState,GridValidator}.swift \
//      ios/tools/grid_audit/main.swift
//    /tmp/grid_audit [nombre_de_jours]
//
//  Sortie : une ligne par jour (twist, métriques, difficulté), puis un résumé.
//  Code de sortie non nul si une grille échoue la validation — utilisable tel
//  quel comme test de non-régression.
//

import Foundation

let days = CommandLine.arguments.count > 1 ? (Int(CommandLine.arguments[1]) ?? 60) : 60
let calendar = Calendar(identifier: .gregorian)
let start = Date()

var failures: [String] = []
var difficulties: [Int] = []
var movesSamples: [Int] = []

print("jour        twist      paires  groupes  coups(méd/pire)  score méd.  cible  difficulté  ms")
print(String(repeating: "-", count: 78))

for offset in 0..<days {
    guard let date = calendar.date(byAdding: .day, value: offset, to: start) else { continue }
    let t0 = Date()
    let today = DailyChallenge.make(for: date)
    let buildMs = Int(Date().timeIntervalSince(t0) * 1000)

    // Évaluation reproductible : graine dérivée du jour audité.
    var rng = SeededGenerator(seed: UInt64(bitPattern: Int64(today.dayKey)))
    let assessment = GridValidator.assess(today.grid, playouts: 5, using: &rng)

    difficulties.append(assessment.difficulty)
    movesSamples.append(assessment.medianMoves)

    let formatter = DateFormatter()
    formatter.dateFormat = "yyyy-MM-dd"
    let label = formatter.string(from: date)
    let twist = String(describing: today.twist).padding(toLength: 10, withPad: " ", startingAt: 0)
    let target = DailyChallenge.Target.forDay(today.dayKey)
    let line = "\(label)  \(twist) "
        + String(format: "%6d   %6d   %6d/%-6d   %8d   %5d   %6d  %4d",
                 assessment.easyPairs, assessment.shortGroups,
                 assessment.medianMoves, assessment.worstMoves,
                 assessment.medianScore, target.rawValue, assessment.difficulty, buildMs)
    print(line + (assessment.isPublishable ? "" : "   ⚠️ REJETÉE"))

    if !assessment.isPublishable {
        failures.append("\(label) (\(today.twist)) — coups pire cas \(assessment.worstMoves), "
                        + "groupes courts \(assessment.shortGroups)")
    }
}

print(String(repeating: "-", count: 78))
difficulties.sort()
movesSamples.sort()
func percentile(_ values: [Int], _ p: Double) -> Int {
    guard !values.isEmpty else { return 0 }
    return values[min(values.count - 1, Int(Double(values.count) * p))]
}
print("difficulté : min \(difficulties.first ?? 0) · médiane \(percentile(difficulties, 0.5)) · max \(difficulties.last ?? 0)")
print("temps de construction d'une grille : voir colonne ms (cible : < 200)")
print("coups méd. : min \(movesSamples.first ?? 0) · médiane \(percentile(movesSamples, 0.5)) · max \(movesSamples.last ?? 0)")

if failures.isEmpty {
    print("\n✅ \(days) jours audités, aucune grille rejetée.")
} else {
    print("\n❌ \(failures.count) grille(s) rejetée(s) sur \(days) :")
    for failure in failures { print("   - \(failure)") }
    exit(1)
}
