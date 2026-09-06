//
//  score_test — vérifie le barème du score (issue #15)
//
//  Compile et exécute le CODE RÉEL du jeu (ScoreRules), pas une copie : le
//  barème vivait déjà en double dans le projet, on ne recommence pas ici.
//
//  Usage :
//    swiftc -O -o /tmp/score_test ios/tenGO/ScoreRules.swift ios/tools/score_test/main.swift
//    /tmp/score_test
//
//  Sort en erreur si un cas échoue — utilisable tel quel comme test de
//  non-régression, en l'absence de target XCTest dans le projet.
//

import Foundation
var failures = 0
func check(_ ok: Bool, _ label: String) {
    print((ok ? "  ok   " : "  ÉCHEC ") + label); if !ok { failures += 1 }
}

print("Barème :")
let expected: [Int: Int] = [0:0, 1:0, 2:10, 3:30, 4:100, 5:200, 6:350, 7:550, 8:800, 9:1100, 10:1450]
for (n, want) in expected.sorted(by: { $0.key < $1.key }) {
    let got = ScoreRules.points(forChain: n)
    check(got == want, "chaîne \(n) → \(got) (attendu \(want))")
}

print("\nPropriétés :")
// Strictement croissant à partir de 2.
var strict = true
for n in 2..<40 where ScoreRules.points(forChain: n + 1) <= ScoreRules.points(forChain: n) { strict = false }
check(strict, "strictement croissant de 2 à 40")

// Superlinéaire : le rendement par bulle ne décroît jamais.
var superlinear = true
for n in 2..<40 {
    let a = Double(ScoreRules.points(forChain: n)) / Double(n)
    let b = Double(ScoreRules.points(forChain: n + 1)) / Double(n + 1)
    if b < a { superlinear = false }
}
check(superlinear, "points par bulle croissant (chaîne longue > somme de courtes)")

// Une chaîne de n vaut plus que n/2 paires : c'est la promesse du GDD.
var beatsPairs = true
for n in 4...20 where ScoreRules.points(forChain: n) <= (n / 2) * ScoreRules.points(forChain: 2) { beatsPairs = false }
check(beatsPairs, "chaîne de n > n/2 paires, pour n de 4 à 20")

// Pas de débordement ni de valeur négative sur une chaîne extrême
// (le plateau fait 63 cellules, donc 63 est la borne physique).
check(ScoreRules.points(forChain: 63) > 0 && ScoreRules.points(forChain: 63) < 1_000_000,
      "chaîne maximale possible (63) reste saine : \(ScoreRules.points(forChain: 63))")

print("\nMultiplicateur affiché :")
for n in [2, 4, 5, 7, 10] {
    print("  chaîne \(n) → ×\(ScoreRules.displayMultiplier(forChain: n))")
}
check(ScoreRules.displayMultiplier(forChain: 2) == 0.5, "paire → ×0.5 par rapport au forfait 10/bulle")

print(failures == 0 ? "\n✅ tous les cas passent" : "\n❌ \(failures) échec(s)")
exit(failures == 0 ? 0 : 1)
