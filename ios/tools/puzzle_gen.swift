#!/usr/bin/env swift
//
//  puzzle_gen.swift
//  tenGO — pipeline de contenu du mode Puzzles (issue #19)
//
//  Génère et VALIDE les niveaux : un niveau n'est retenu que s'il est
//  démontré entièrement solvable, et ses seuils d'étoiles sont dérivés du
//  meilleur score réellement atteignable (calculé, pas deviné).
//
//  Les règles sont recopiées à l'identique de GridModel.swift et de
//  GameScene.scoreForPath — toute divergence invaliderait les niveaux.
//
//  Usage :  swift ios/tools/puzzle_gen.swift > ios/tenGO/PuzzleCatalog.swift
//

import Foundation

// MARK: - Règles du jeu (miroir de GridModel)

let kRows = 9
let kCols = 7

/// Barème de GameScene.scoreForPath — superlinéaire : c'est lui qui fait
/// qu'un même niveau peut rapporter beaucoup plus si on construit de longues
/// chaînes plutôt que des paires.
func scoreForPath(length: Int) -> Int {
    switch length {
    case 2: return 10
    case 3: return 30
    case 4: return 100
    default: return 100 + 50 * (length - 4)
    }
}

/// RNG déterministe — même algorithme que SeededGenerator.swift (SplitMix64).
struct Seeded: RandomNumberGenerator {
    var state: UInt64
    init(seed: UInt64) { state = seed }
    mutating func next() -> UInt64 {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}

/// Un niveau : par colonne, les valeurs de bas en haut (la ligne 0 est le bas
/// de l'écran). Toutes les bulles reposent sur le fond, donc la gravité est
/// déjà au repos au premier affichage.
struct Layout {
    var columns: [[Int]]

    var bubbleCount: Int { columns.reduce(0) { $0 + $1.count } }
    var total: Int { columns.reduce(0) { $0 + $1.reduce(0, +) } }

    /// Identifiants stables des bulles : bit (colonne, rang dans la colonne).
    /// La gravité conserve l'ordre relatif dans une colonne et ne change jamais
    /// de colonne : l'ensemble des bulles restantes détermine donc entièrement
    /// la disposition — d'où la mémoïsation par masque de bits.
    func bitIndex(col: Int, rank: Int) -> Int { col * kRows + rank }
}

/// Positions courantes (row, col, value, bit) pour un masque de bulles restantes.
func layoutCells(_ layout: Layout, remaining: UInt64) -> [(row: Int, col: Int, value: Int, bit: UInt64)] {
    var out: [(row: Int, col: Int, value: Int, bit: UInt64)] = []
    for c in 0..<layout.columns.count {
        var row = 0
        for (rank, value) in layout.columns[c].enumerated() {
            let bit = UInt64(1) << layout.bitIndex(col: c, rank: rank)
            guard remaining & bit != 0 else { continue }
            out.append((row: row, col: c, value: value, bit: bit))
            row += 1     // compaction par gravité vers la ligne 0
        }
    }
    return out
}

/// Tous les GROUPES distincts de somme 10 formant un chemin adjacent
/// (8 directions), dédupliqués par masque — même logique que
/// GridModel.countSumTenGroups, mais sans plafond de longueur.
func sumTenMoves(_ layout: Layout, remaining: UInt64) -> [(mask: UInt64, length: Int)] {
    let cells = layoutCells(layout, remaining: remaining)
    guard !cells.isEmpty else { return [] }

    var byPos: [Int: (value: Int, bit: UInt64)] = [:]
    for c in cells { byPos[c.row * kCols + c.col] = (c.value, c.bit) }

    var found: [UInt64: Int] = [:]

    func explore(row: Int, col: Int, sum: Int, length: Int, mask: UInt64) {
        if sum == 10 {
            found[mask] = length
            return
        }
        if sum > 10 { return }
        for dr in -1...1 {
            for dc in -1...1 {
                guard dr != 0 || dc != 0 else { continue }
                let nr = row + dr, nc = col + dc
                guard nr >= 0, nr < kRows, nc >= 0, nc < kCols else { continue }
                guard let n = byPos[nr * kCols + nc] else { continue }
                guard mask & n.bit == 0 else { continue }
                explore(row: nr, col: nc, sum: sum + n.value, length: length + 1, mask: mask | n.bit)
            }
        }
    }

    for c in cells {
        explore(row: c.row, col: c.col, sum: c.value, length: 1, mask: c.bit)
    }
    return found.map { (mask: $0.key, length: $0.value) }
}

// MARK: - Solveur

/// Meilleur score atteignable en vidant ENTIÈREMENT la grille, ou nil si
/// l'état ne peut pas être vidé. Mémoïsé par masque de bulles restantes.
final class Solver {
    private var memo: [UInt64: Int?] = [:]
    private var budget: Int
    private(set) var exhausted = false
    let layout: Layout

    init(layout: Layout, budget: Int = 400_000) {
        self.layout = layout
        self.budget = budget
    }

    func bestScore(remaining: UInt64) -> Int? {
        if remaining == 0 { return 0 }
        if let cached = memo[remaining] { return cached }
        if budget <= 0 { exhausted = true; return nil }
        budget -= 1

        var best: Int? = nil
        for move in sumTenMoves(layout, remaining: remaining) {
            guard let sub = bestScore(remaining: remaining & ~move.mask) else { continue }
            let candidate = sub + scoreForPath(length: move.length)
            if candidate > (best ?? Int.min) { best = candidate }
        }
        memo[remaining] = best
        return best
    }
}

// MARK: - Génération

func fullMask(_ layout: Layout) -> UInt64 {
    var m: UInt64 = 0
    for c in 0..<layout.columns.count {
        for rank in 0..<layout.columns[c].count {
            m |= UInt64(1) << layout.bitIndex(col: c, rank: rank)
        }
    }
    return m
}

/// Fabrique une disposition candidate : quelques colonnes remplies depuis le
/// bas, puis ajustement de la dernière valeur pour que la somme soit un
/// multiple de 10 (condition NÉCESSAIRE : chaque coup retire exactement 10).
func makeCandidate(seed: UInt64, targetBubbles: Int, maxHeight: Int) -> Layout? {
    var rng = Seeded(seed: seed)
    var columns = [[Int]](repeating: [], count: kCols)

    // Réparti sur des colonnes contiguës pour garder les bulles adjacentes.
    var remaining = targetBubbles
    let firstCol = Int.random(in: 0...(kCols - 3), using: &rng)
    var col = firstCol
    while remaining > 0 && col < kCols {
        let height = min(remaining, Int.random(in: 1...maxHeight, using: &rng))
        columns[col] = (0..<height).map { _ in Int.random(in: 1...9, using: &rng) }
        remaining -= height
        col += 1
    }
    guard remaining == 0 else { return nil }

    var layout = Layout(columns: columns)
    // Ajuste la dernière bulle pour rendre la somme divisible par 10.
    let missing = (10 - (layout.total % 10)) % 10
    guard let lastCol = (0..<kCols).last(where: { !layout.columns[$0].isEmpty }) else { return nil }
    let lastIndex = layout.columns[lastCol].count - 1
    let adjusted = layout.columns[lastCol][lastIndex] + missing
    layout.columns[lastCol][lastIndex] = adjusted > 9 ? adjusted - 10 : adjusted
    guard layout.columns[lastCol][lastIndex] >= 1 else { return nil }
    guard layout.total % 10 == 0 else { return nil }
    return layout
}

struct GeneratedLevel {
    let index: Int
    let layout: Layout
    let bestScore: Int
    let moves: Int
}

/// Cherche un niveau solvable pour l'index donné, en essayant des graines
/// successives. Retourne nil si aucune graine ne convient dans la fenêtre.
func generateLevel(index: Int, targetBubbles: Int, maxHeight: Int, seedBase: UInt64) -> GeneratedLevel? {
    for offset in 0..<4000 {
        let seed = seedBase &+ UInt64(index) &* 7919 &+ UInt64(offset)
        guard let layout = makeCandidate(seed: seed, targetBubbles: targetBubbles, maxHeight: maxHeight) else { continue }
        let solver = Solver(layout: layout)
        guard let best = solver.bestScore(remaining: fullMask(layout)), !solver.exhausted else { continue }
        return GeneratedLevel(index: index,
                              layout: layout,
                              bestScore: best,
                              moves: layout.total / 10)
    }
    return nil
}

// MARK: - Sortie

/// Encodage compact : colonnes séparées par « | », valeurs de bas en haut.
/// Les colonnes remplies sont recentrées horizontalement — un décalage
/// uniforme préserve l'adjacence, donc la solvabilité démontrée plus haut.
func encode(_ layout: Layout) -> String {
    let filled = (0..<kCols).filter { !layout.columns[$0].isEmpty }
    var columns = layout.columns
    if let first = filled.first, let last = filled.last {
        let width = last - first + 1
        let shift = (kCols - width) / 2 - first
        if shift != 0 {
            var moved = [[Int]](repeating: [], count: kCols)
            for c in filled where c + shift >= 0 && c + shift < kCols {
                moved[c + shift] = layout.columns[c]
            }
            columns = moved
        }
    }
    return columns.map { $0.map(String.init).joined() }.joined(separator: "|")
}

// World 1 « Learning » : 20 niveaux, difficulté croissante par le nombre de
// bulles (donc de coups) et la hauteur des piles.
var levels: [GeneratedLevel] = []
for index in 1...20 {
    let bubbles: Int
    let maxHeight: Int
    switch index {
    case 1...5:   bubbles = 8 + index;        maxHeight = 3
    case 6...12:  bubbles = 12 + (index - 5); maxHeight = 4
    default:      bubbles = 18 + (index - 12) / 2; maxHeight = 5
    }
    guard let level = generateLevel(index: index,
                                    targetBubbles: bubbles,
                                    maxHeight: maxHeight,
                                    seedBase: 0x7E_10_60) else {
        FileHandle.standardError.write("échec de génération pour le niveau \(index)\n".data(using: .utf8)!)
        exit(1)
    }
    levels.append(level)
}

print("""
//
//  PuzzleCatalog.swift
//  tenGO
//
//  ⚠️ FICHIER GÉNÉRÉ — ne pas éditer à la main.
//  Produit par `swift ios/tools/puzzle_gen.swift`, qui VALIDE chaque niveau :
//  la solvabilité complète est démontrée par recherche exhaustive, et les
//  seuils d'étoiles dérivent du meilleur score réellement atteignable.
//
//  Format : colonnes séparées par « | », valeurs de bas en haut
//  (la ligne 0 est le bas de l'écran).
//

enum PuzzleCatalog {

    static let world1: [PuzzleLevel] = [
""")
for level in levels {
    // 3★ exigeant mais atteignable, 2★ confortable. Arrondis à la dizaine
    // pour rester lisibles à l'écran.
    let three = (Int(Double(level.bestScore) * 0.85) / 10) * 10
    let two = (Int(Double(level.bestScore) * 0.60) / 10) * 10
    print("        PuzzleLevel(index: \(level.index), world: 1, layout: \"\(encode(level.layout))\", "
        + "moves: \(level.moves), twoStarScore: \(two), threeStarScore: \(three)),")
}
print("""
    ]
}
""")

FileHandle.standardError.write("généré : \(levels.count) niveaux\n".data(using: .utf8)!)
for level in levels {
    let line = "  niveau \(level.index) — \(level.layout.bubbleCount) bulles, "
        + "\(level.moves) coups, meilleur score \(level.bestScore)\n"
    FileHandle.standardError.write(line.data(using: .utf8)!)
}
