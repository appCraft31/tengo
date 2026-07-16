//
//  SeededGenerator.swift
//  tenGO
//
//  Générateur pseudo-aléatoire déterministe (SplitMix64).
//  Permet de reproduire à l'identique une grille à partir d'une graine —
//  base du Défi du jour, où tous les joueurs partagent la même grille.
//

/// RNG déterministe : une même graine produit toujours la même suite.
/// Implémente l'algorithme SplitMix64 (rapide, bonne distribution).
struct SeededGenerator: RandomNumberGenerator {
    private var state: UInt64

    init(seed: UInt64) {
        state = seed
    }

    mutating func next() -> UInt64 {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}
