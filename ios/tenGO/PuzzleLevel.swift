//
//  PuzzleLevel.swift
//  tenGO
//
//  Mode Puzzles : niveaux à disposition fixe, objectif « vider la grille ».
//
//  Note de conception — pourquoi les étoiles portent sur le SCORE et non sur
//  un « par » en nombre de coups comme le prévoyait le GDD (§13-15) : dans
//  tenGO, chaque coup retire exactement 10 points de valeur. Vider une grille
//  demande donc TOUJOURS somme/10 coups, quel que soit le jeu du joueur — un
//  par en coups ne peut rien départager. Ce qui varie, c'est la façon de
//  découper ces 10 points : le barème de score est superlinéaire (4 bulles
//  valent 100 points là où deux paires n'en valent que 20). Les étoiles
//  récompensent donc l'élégance du découpage, ce qui préserve exactement
//  l'intention du GDD — rejouer un niveau pour faire mieux.
//

import Foundation

struct PuzzleLevel {
    let index: Int
    let world: Int
    /// Colonnes séparées par « | », valeurs de bas en haut (ligne 0 = bas).
    let layout: String
    /// Nombre de coups nécessaires pour vider la grille (invariant : somme/10).
    let moves: Int
    let twoStarScore: Int
    let threeStarScore: Int

    /// Étoiles obtenues pour un score donné, la grille ayant été vidée.
    func stars(forScore score: Int) -> Int {
        if score >= threeStarScore { return 3 }
        if score >= twoStarScore { return 2 }
        return 1
    }
}

enum PuzzleWorld {
    static let all = [1]

    static func levels(inWorld world: Int) -> [PuzzleLevel] {
        switch world {
        case 1: return PuzzleCatalog.world1
        default: return []
        }
    }

    static func level(world: Int, index: Int) -> PuzzleLevel? {
        levels(inWorld: world).first { $0.index == index }
    }

    static func nameKey(forWorld world: Int) -> String { "puzzle.world\(world)" }
}

/// Progression du joueur : meilleures étoiles par niveau. Un niveau est
/// déverrouillé dès que le précédent a au moins une étoile ; le premier l'est
/// toujours. Persistance locale (UserDefaults), aucun backend.
final class PuzzleProgress {

    static let shared = PuzzleProgress()
    private init() {}

    private let defaults = UserDefaults.standard

    private func key(world: Int, index: Int) -> String {
        "\(AppConfig.UserDefaultsKey.puzzleStarsPrefix)\(world)_\(index)"
    }

    /// Étoiles obtenues sur un niveau (0 = jamais terminé).
    func stars(world: Int, index: Int) -> Int {
        defaults.integer(forKey: key(world: world, index: index))
    }

    /// Enregistre un résultat. Les étoiles ne redescendent jamais : rejouer
    /// un niveau ne peut qu'améliorer le score obtenu.
    func record(world: Int, index: Int, stars: Int) {
        guard stars > self.stars(world: world, index: index) else { return }
        defaults.set(stars, forKey: key(world: world, index: index))
    }

    func isUnlocked(world: Int, index: Int) -> Bool {
        index <= 1 || stars(world: world, index: index - 1) > 0
    }

    func totalStars(inWorld world: Int) -> Int {
        PuzzleWorld.levels(inWorld: world).reduce(0) { $0 + stars(world: world, index: $1.index) }
    }

    func completedCount(inWorld world: Int) -> Int {
        PuzzleWorld.levels(inWorld: world).filter { stars(world: world, index: $0.index) > 0 }.count
    }
}
