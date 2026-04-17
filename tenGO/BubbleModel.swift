//
//  BubbleModel.swift
//  tenGO
//

struct BubbleModel: Equatable, Codable {
    let value: Int  // 1–9
    var row: Int
    var col: Int

    static func == (lhs: BubbleModel, rhs: BubbleModel) -> Bool {
        lhs.row == rhs.row && lhs.col == rhs.col
    }
}
