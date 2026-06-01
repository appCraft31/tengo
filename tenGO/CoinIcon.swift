//
//  CoinIcon.swift
//  tenGO
//
//  Pièce vectorielle (SKShapeNode) réutilisée pour le solde et les prix.
//

import SpriteKit

enum CoinIcon {
    static func make(radius r: CGFloat) -> SKNode {
        let coin = SKNode()
        let gold = UIColor(red: 0.98, green: 0.84, blue: 0.46, alpha: 1)
        let amber = UIColor(red: 0.84, green: 0.64, blue: 0.28, alpha: 1)

        let disc = SKShapeNode(circleOfRadius: r)
        disc.fillColor = gold
        disc.strokeColor = amber
        disc.lineWidth = max(1, r * 0.14)
        coin.addChild(disc)

        let rim = SKShapeNode(circleOfRadius: r - max(2, r * 0.3))
        rim.fillColor = .clear
        rim.strokeColor = amber.withAlphaComponent(0.5)
        rim.lineWidth = 1
        coin.addChild(rim)

        let glint = SKShapeNode(circleOfRadius: r * 0.28)
        glint.fillColor = UIColor(white: 1.0, alpha: 0.55)
        glint.strokeColor = .clear
        glint.position = CGPoint(x: -r * 0.32, y: r * 0.32)
        coin.addChild(glint)

        return coin
    }
}
