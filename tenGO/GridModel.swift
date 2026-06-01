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

    init() {
        var generator = SystemRandomNumberGenerator()
        cells = GridModel.generateCells(using: &generator)
    }

    /// Génère une grille reproductible à partir d'un RNG injecté (Défi du jour).
    init(using generator: inout some RandomNumberGenerator) {
        cells = GridModel.generateCells(using: &generator)
    }

    init(from state: GameState) {
        cells = state.cells
    }

    private init(cells: [[BubbleModel?]]) {
        self.cells = cells
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
        cells.allSatisfy { row in row.allSatisfy { $0 == nil } }
    }

    // DFS: does any path of sum exactly 10 exist?
    func hasValidMove() -> Bool {
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let bubble = cells[row][col] else { continue }
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
                guard let neighbor = cells[nr][nc] else { continue }
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

    // Compact each column downward; returns movements for animation.
    mutating func applyGravity() -> [(model: BubbleModel, fromRow: Int, toRow: Int, col: Int)] {
        var movements: [(model: BubbleModel, fromRow: Int, toRow: Int, col: Int)] = []

        for col in 0..<GridModel.cols {
            var nonNil: [(row: Int, model: BubbleModel)] = []
            for row in 0..<GridModel.rows {
                if let model = cells[row][col] {
                    nonNil.append((row: row, model: model))
                }
            }
            for row in 0..<GridModel.rows { cells[row][col] = nil }
            for (newRow, item) in nonNil.enumerated() {
                var model = item.model
                model.row = newRow
                cells[newRow][col] = model
                if item.row != newRow {
                    movements.append((model: model, fromRow: item.row, toRow: newRow, col: col))
                }
            }
        }
        return movements
    }
}
