//
//  puzzle_solve — calcule la solution qui VIDE un niveau de Puzzles.
//
//  Compile et exécute le CODE RÉEL du jeu (GridModel, PuzzleCatalog,
//  ScoreRules) : la solution imprimée est jouable telle quelle par la démo.
//
//  Pourquoi cet outil existe : l'auto-player de la démo est glouton (il prend
//  la plus longue chaîne disponible) et ne vide qu'UN niveau du monde 1 sur
//  vingt. Une vidéo « ce niveau semblait impossible » exige donc la vraie
//  solution, calculée hors ligne par recherche exhaustive mémoïsée.
//
//  Usage :
//    swiftc -O -o /tmp/puzzle_solve \
//      ios/tenGO/GridModel.swift ios/tenGO/BubbleModel.swift ios/tenGO/SeededGenerator.swift \
//      ios/tenGO/PuzzleLevel.swift ios/tenGO/PuzzleCatalog.swift ios/tenGO/ScoreRules.swift \
//      ios/tenGO/AppConfig.swift ios/tenGO/GameState.swift ios/tools/puzzle_solve/main.swift
//    /tmp/puzzle_solve 19        # un niveau
//    /tmp/puzzle_solve           # les 20 niveaux du monde 1
//
//  Sortie : une ligne par niveau, avec la suite de coups au format attendu par
//  `DEMO_MOVES` (coups séparés par « | », cellules par « ; », ligne 0 en bas,
//  coordonnées exprimées APRÈS la gravité du coup précédent).
//

import Foundation

/// Un coup : les cases à relier, dans l'ordre du tracé.
typealias Move = [(row: Int, col: Int)]

/// État compact d'une grille : 63 cases, valeur 0 = vide. Sert de clé de
/// mémoïsation — deux chemins de coups différents menant au même plateau
/// partagent le même sous-arbre.
private func key(of grid: GridModel) -> String {
    var out = ""
    for row in 0..<GridModel.rows {
        for col in 0..<GridModel.cols {
            out.append(Character(UnicodeScalar(48 + (grid.cells[row][col]?.value ?? 0))!))
        }
    }
    return out
}

private struct Solution {
    let moves: [Move]
    let score: Int
}

private var memo: [String: Solution?] = [:]
private var explored = 0

/// Meilleure solution VIDANT la grille, ou nil si aucune n'existe.
/// « Meilleure » = score le plus élevé : c'est le critère du jeu, et il pousse
/// mécaniquement vers les longues chaînes, donc vers les coups spectaculaires.
private func solve(_ grid: GridModel) -> Solution? {
    if grid.isGridEmpty() { return Solution(moves: [], score: 0) }
    let k = key(of: grid)
    if let cached = memo[k] { return cached }
    memo[k] = Solution?.none          // coupe les cycles pendant la descente
    explored += 1

    var best: Solution?
    // maxLen 9 : on ne s'interdit aucune longueur, c'est la recherche qui tranche.
    for move in grid.sumTenGroups(maxLen: 9) {
        var next = grid
        next.removeBubbles(at: move)
        _ = next.thawFrozenBubbles(adjacentTo: move)
        _ = next.applyGravity()
        guard let rest = solve(next) else { continue }
        let score = ScoreRules.points(forChain: move.count) + rest.score
        if best == nil || score > best!.score {
            best = Solution(moves: [move] + rest.moves, score: score)
        }
    }
    memo[k] = best
    return best
}

private func format(_ moves: [Move]) -> String {
    moves.map { move in
        move.map { "\($0.row),\($0.col)" }.joined(separator: ";")
    }.joined(separator: "|")
}

// ---------------------------------------------------------------- exécution

let requested = CommandLine.arguments.dropFirst().compactMap(Int.init)
let levels = requested.isEmpty ? PuzzleWorld.levels(inWorld: 1) :
    requested.compactMap { PuzzleWorld.level(world: 1, index: $0) }

var failures = 0
for level in levels {
    memo.removeAll(); explored = 0
    let grid = GridModel(puzzleLayout: level.layout)
    let started = Date()
    guard let solution = solve(grid) else {
        print("niveau \(level.index) : AUCUNE solution vidant la grille")
        failures += 1
        continue
    }
    let elapsed = Date().timeIntervalSince(started)
    // Garde-fou : la solution doit vider le plateau en `moves` coups (chaque
    // coup retire exactement 10 points de valeur) et décrocher les 3 étoiles.
    let stars = level.stars(forScore: solution.score)
    let coupsOK = solution.moves.count == level.moves
    print("niveau \(level.index) : \(solution.moves.count) coups (catalogue \(level.moves))"
          + " · \(solution.score) pts · \(stars)★"
          + (coupsOK && stars == 3 ? "" : "  ⚠ ATTENDU \(level.moves) coups et 3★")
          + " · \(explored) états en \(String(format: "%.1f", elapsed))s")
    print("  DEMO_MOVES=\"\(format(solution.moves))\"")
    if !coupsOK || stars != 3 { failures += 1 }
}

if failures > 0 {
    FileHandle.standardError.write("\(failures) niveau(x) en échec\n".data(using: .utf8)!)
    exit(1)
}
