//
//  GameState.swift
//  tenGO
//

import Foundation

struct GameState: Codable {
    let cells: [[BubbleModel?]]
    let score: Int
    /// Cases-obstacles du Défi du jour. Optionnel : absent des sauvegardes
    /// antérieures aux twists (mode normal → masque vide au chargement).
    let blocked: [[Bool]]?

    private static let savedGameKey = "tengo_saved_game"
    private static let highScoresKey = "tengo_high_scores"

    // MARK: - Persistence

    static func save(gridModel: GridModel, score: Int) {
        let state = GameState(cells: gridModel.cells, score: score, blocked: gridModel.blocked)
        if let data = try? JSONEncoder().encode(state) {
            UserDefaults.standard.set(data, forKey: savedGameKey)
        }
    }

    static func load() -> GameState? {
        guard let data = UserDefaults.standard.data(forKey: savedGameKey),
              let state = try? JSONDecoder().decode(GameState.self, from: data)
        else { return nil }
        return state
    }

    static func clear() {
        UserDefaults.standard.removeObject(forKey: savedGameKey)
    }

    static var exists: Bool {
        UserDefaults.standard.data(forKey: savedGameKey) != nil
    }

    // MARK: - Leaderboard

    static func addScore(_ score: Int) {
        var scores = highScores()
        scores.append(score)
        scores.sort(by: >)
        UserDefaults.standard.set(Array(scores.prefix(10)), forKey: highScoresKey)
    }

    static func highScores() -> [Int] {
        UserDefaults.standard.array(forKey: highScoresKey) as? [Int] ?? []
    }
}
