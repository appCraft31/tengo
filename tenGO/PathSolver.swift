//
//  PathSolver.swift
//  tenGO
//
//  Solveur de chemins « somme = 10 » utilisé par le mode démo (auto-player).
//  Cherche, à partir de l'état courant de la grille, un chemin adjacent
//  (king-moves) dont la somme vaut exactement 10 — en privilégiant les chemins
//  les plus spectaculaires (longs = gros combos, gros score) pour une capture
//  vidéo énergique. Purement déterministe : ne dépend que de la grille.
//

extension GridModel {

    /// Trouve un chemin valide (somme 10, cellules adjacentes, non gelées) à jouer.
    ///
    /// Heuristique « show » : on retourne le chemin **le plus long** possible
    /// (≤ `maxLen`) — il produit le trail le plus long, le plus gros pop de score
    /// et vide davantage la grille. À longueur égale, l'ordre de balayage tranche
    /// (déterministe). Retourne `nil` si aucun coup n'existe.
    func showcasePath(maxLen: Int = 5) -> [(row: Int, col: Int)]? {
        var best: [Int]? = nil           // chemin encodé en index linéaires r*cols+c
        var path: [Int] = []
        var visited = Set<Int>()

        func consider() {
            if best == nil || path.count > best!.count {
                best = path
            }
        }

        func dfs(row: Int, col: Int, sum: Int) {
            if sum == 10 { consider(); return }
            if sum > 10 || path.count >= maxLen { return }
            for dr in -1...1 {
                for dc in -1...1 {
                    guard dr != 0 || dc != 0 else { continue }
                    let nr = row + dr, nc = col + dc
                    guard nr >= 0, nr < GridModel.rows, nc >= 0, nc < GridModel.cols else { continue }
                    guard let nb = cells[nr][nc], !nb.isFrozen else { continue }
                    let key = nr * GridModel.cols + nc
                    guard !visited.contains(key) else { continue }
                    visited.insert(key)
                    path.append(key)
                    dfs(row: nr, col: nc, sum: sum + nb.value)
                    path.removeLast()
                    visited.remove(key)
                }
            }
        }

        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let b = cells[row][col], !b.isFrozen else { continue }
                let key = row * GridModel.cols + col
                visited = [key]
                path = [key]
                dfs(row: row, col: col, sum: b.value)
            }
        }

        return best?.map { (row: $0 / GridModel.cols, col: $0 % GridModel.cols) }
    }
}
