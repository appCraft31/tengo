//
//  GridModel.swift
//  tenGO
//

private struct GridPos: Hashable {
    let row, col: Int
}

struct GridModel {
    static let rows = 9
    static let cols = 7

    private(set) var cells: [[BubbleModel?]]

    /// Défi du jour — twist « obstacle » : cases bloquées par une pierre inerte.
    /// Une case bloquée ne contient pas de bulle, casse l'adjacence du tracé
    /// et arrête la chute des bulles (segmente la colonne pour la gravité).
    private(set) var blocked: [[Bool]]

    init() {
        var generator = SystemRandomNumberGenerator()
        cells = GridModel.generateCells(using: &generator)
        blocked = GridModel.emptyMask()
    }

    /// Génère une grille reproductible à partir d'un RNG injecté (Défi du jour).
    init(using generator: inout some RandomNumberGenerator) {
        cells = GridModel.generateCells(using: &generator)
        blocked = GridModel.emptyMask()
    }

    init(from state: GameState) {
        cells = state.cells
        blocked = state.blocked ?? GridModel.emptyMask()
    }

    private init(cells: [[BubbleModel?]], blocked: [[Bool]]? = nil) {
        self.cells = cells
        self.blocked = blocked ?? GridModel.emptyMask()
    }

    private static func emptyMask() -> [[Bool]] {
        Array(repeating: Array(repeating: false, count: cols), count: rows)
    }

    // MARK: - Generation

    private static func generateCells<G: RandomNumberGenerator>(using generator: inout G) -> [[BubbleModel?]] {
        for _ in 0..<100 {
            let candidate = makeCells(using: &generator)
            if GridModel(cells: candidate).hasValidMove() {
                return candidate
            }
        }
        // Fallback: force at least one valid pair
        var fallback = makeCells(using: &generator)
        fallback[0][0] = BubbleModel(value: 5, row: 0, col: 0)
        fallback[0][1] = BubbleModel(value: 5, row: 0, col: 1)
        return fallback
    }

    private static func makeCells<G: RandomNumberGenerator>(using generator: inout G) -> [[BubbleModel?]] {
        (0..<rows).map { row in
            (0..<cols).map { col in
                BubbleModel(value: Int.random(in: 1...9, using: &generator), row: row, col: col) as BubbleModel?
            }
        }
    }

    // MARK: - Twist placement (Défi du jour)

    /// Toutes les positions actuellement occupées par une bulle.
    private func bubblePositions() -> [GridPos] {
        var positions: [GridPos] = []
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols where cells[row][col] != nil {
                positions.append(GridPos(row: row, col: col))
            }
        }
        return positions
    }

    /// Twist « obstacle » : transforme `count` cases en pierres inertes.
    mutating func placeObstacles<G: RandomNumberGenerator>(count: Int, using generator: inout G) {
        let targets = bubblePositions().shuffled(using: &generator).prefix(count)
        for pos in targets {
            cells[pos.row][pos.col] = nil
            blocked[pos.row][pos.col] = true
        }
    }

    /// Twist « ancrée » : `count` bulles ne tomberont pas avec la gravité.
    mutating func anchorBubbles<G: RandomNumberGenerator>(count: Int, using generator: inout G) {
        let targets = bubblePositions().shuffled(using: &generator).prefix(count)
        for pos in targets {
            cells[pos.row][pos.col]?.isAnchored = true
        }
    }

    /// Twist « gelée » : `count` bulles sont givrées (inutilisables jusqu'à fonte).
    mutating func freezeBubbles<G: RandomNumberGenerator>(count: Int, using generator: inout G) {
        let targets = bubblePositions().shuffled(using: &generator).prefix(count)
        for pos in targets {
            cells[pos.row][pos.col]?.isFrozen = true
        }
    }

    // MARK: - Queries

    func isAdjacent(_ a: (row: Int, col: Int), _ b: (row: Int, col: Int)) -> Bool {
        let dr = abs(a.row - b.row)
        let dc = abs(a.col - b.col)
        return dr <= 1 && dc <= 1 && (dr != 0 || dc != 0)
    }

    func pathSum(_ path: [(row: Int, col: Int)]) -> Int {
        path.compactMap { cells[$0.row][$0.col]?.value }.reduce(0, +)
    }

    func isGridEmpty() -> Bool {
        // « Vide » = plus aucune bulle. Les obstacles (cases bloquées) ne comptent pas.
        cells.allSatisfy { row in row.allSatisfy { $0 == nil } }
    }

    /// Critère de validation du Défi du jour : au moins un coup jouable existe
    /// (même garantie que le mode normal, en tenant compte des twists).
    func isSolvable() -> Bool {
        hasValidMove()
    }

    // DFS: does any path of sum exactly 10 exist? (les bulles gelées sont intraversables)
    func hasValidMove() -> Bool {
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let bubble = cells[row][col], !bubble.isFrozen else { continue }
                var visited = Set<GridPos>()
                visited.insert(GridPos(row: row, col: col))
                if dfs(row: row, col: col, sum: bubble.value, visited: &visited) {
                    return true
                }
            }
        }
        return false
    }

    private func dfs(row: Int, col: Int, sum: Int, visited: inout Set<GridPos>) -> Bool {
        if sum == 10 { return true }
        if sum > 10 { return false }

        for dr in -1...1 {
            for dc in -1...1 {
                guard dr != 0 || dc != 0 else { continue }
                let nr = row + dr, nc = col + dc
                guard nr >= 0 && nr < GridModel.rows else { continue }
                guard nc >= 0 && nc < GridModel.cols else { continue }
                guard let neighbor = cells[nr][nc], !neighbor.isFrozen else { continue }
                let pos = GridPos(row: nr, col: nc)
                guard !visited.contains(pos) else { continue }
                visited.insert(pos)
                if dfs(row: nr, col: nc, sum: sum + neighbor.value, visited: &visited) {
                    return true
                }
                visited.remove(pos)
            }
        }
        return false
    }

    // MARK: - Mutations

    mutating func removeBubbles(at path: [(row: Int, col: Int)]) {
        for pos in path {
            cells[pos.row][pos.col] = nil
        }
    }

    /// Fait fondre le givre des bulles gelées adjacentes au chemin qui vient
    /// d'être validé. Retourne les positions dégelées (pour l'animation).
    mutating func thawFrozenBubbles(adjacentTo path: [(row: Int, col: Int)]) -> [(row: Int, col: Int)] {
        var thawed: [(row: Int, col: Int)] = []
        var seen = Set<GridPos>()
        for step in path {
            for dr in -1...1 {
                for dc in -1...1 {
                    guard dr != 0 || dc != 0 else { continue }
                    let nr = step.row + dr, nc = step.col + dc
                    guard nr >= 0 && nr < GridModel.rows else { continue }
                    guard nc >= 0 && nc < GridModel.cols else { continue }
                    let pos = GridPos(row: nr, col: nc)
                    guard !seen.contains(pos) else { continue }
                    seen.insert(pos)
                    if cells[nr][nc]?.isFrozen == true {
                        cells[nr][nc]?.isFrozen = false
                        thawed.append((row: nr, col: nc))
                    }
                }
            }
        }
        return thawed
    }

    // Compact each column downward; returns movements for animation.
    // Les bulles ancrées restent en place ; les obstacles segmentent la colonne.
    mutating func applyGravity() -> [(model: BubbleModel, fromRow: Int, toRow: Int, col: Int)] {
        var movements: [(model: BubbleModel, fromRow: Int, toRow: Int, col: Int)] = []

        for col in 0..<GridModel.cols {
            var newCol: [BubbleModel?] = Array(repeating: nil, count: GridModel.rows)

            // Les bulles ancrées conservent leur position (et bornent les segments).
            for row in 0..<GridModel.rows {
                if let b = cells[row][col], b.isAnchored {
                    newCol[row] = b
                }
            }

            // Compacte les bulles non ancrées vers le bas, segment par segment.
            // Une barrière = case bloquée (obstacle) ou bulle ancrée.
            func isBarrier(_ row: Int) -> Bool {
                blocked[row][col] || (cells[row][col]?.isAnchored ?? false)
            }

            var row = GridModel.rows - 1
            while row >= 0 {
                if isBarrier(row) {
                    row -= 1
                    continue
                }
                // Limite haute du segment courant (cases non-barrières contiguës).
                var top = row
                while top - 1 >= 0 && !isBarrier(top - 1) {
                    top -= 1
                }
                let bottom = row

                // Bulles non ancrées du segment, dans l'ordre haut → bas.
                var falling: [BubbleModel] = []
                for r in top...bottom {
                    if let b = cells[r][col], !b.isAnchored {
                        falling.append(b)
                    }
                }
                // Empilées au bas du segment (ligne 0 = bas de l'écran), ordre relatif conservé.
                for (i, model) in falling.enumerated() {
                    let toRow = top + i
                    var placed = model
                    let fromRow = placed.row
                    placed.row = toRow
                    newCol[toRow] = placed
                    if fromRow != toRow {
                        movements.append((model: placed, fromRow: fromRow, toRow: toRow, col: col))
                    }
                }
                row = top - 1
            }

            for r in 0..<GridModel.rows {
                cells[r][col] = newCol[r]
            }
        }
        return movements
    }
}
