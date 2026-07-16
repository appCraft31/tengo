//
//  BubbleModel.swift
//  tenGO
//

struct BubbleModel: Equatable, Codable {
    let value: Int  // 1–9
    var row: Int
    var col: Int

    /// Défi du jour — twist « ancrée » : la bulle ne tombe pas avec la gravité.
    var isAnchored: Bool = false
    /// Défi du jour — twist « gelée » : inutilisable tant que le givre n'a pas
    /// fondu (un chemin validé adjacent la dégèle).
    var isFrozen: Bool = false

    init(value: Int, row: Int, col: Int, isAnchored: Bool = false, isFrozen: Bool = false) {
        self.value = value
        self.row = row
        self.col = col
        self.isAnchored = isAnchored
        self.isFrozen = isFrozen
    }

    // Décodage tolérant : les sauvegardes antérieures aux twists ne contiennent
    // pas isAnchored/isFrozen — on retombe sur false sans casser le chargement.
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        value = try c.decode(Int.self, forKey: .value)
        row = try c.decode(Int.self, forKey: .row)
        col = try c.decode(Int.self, forKey: .col)
        isAnchored = try c.decodeIfPresent(Bool.self, forKey: .isAnchored) ?? false
        isFrozen = try c.decodeIfPresent(Bool.self, forKey: .isFrozen) ?? false
    }

    static func == (lhs: BubbleModel, rhs: BubbleModel) -> Bool {
        lhs.row == rhs.row && lhs.col == rhs.col
    }
}
