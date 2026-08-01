//
//  FXTextures.swift
//  tenGO
//
//  Textures procédurales partagées par les effets.
//
//  Raison d'être : chaque SKShapeNode est un draw call non batché. Un pop de
//  6 bulles en produisait une centaine. Toutes les particules partagent
//  désormais UNE texture, donc SpriteKit peut les regrouper (ignoresSiblingOrder
//  est actif). On peut ainsi enrichir les éclats tout en réduisant le coût.
//
//  Aucun asset n'est ajouté au projet : tout est généré au premier accès.
//

import SpriteKit
import UIKit

enum FXTextures {

    /// Disque doux blanc (dégradé radial opaque → transparent).
    /// Teinter via `color` + `colorBlendFactor = 1`.
    static let dot: SKTexture = makeSoftDot(diameter: 32)

    /// Anneau fin, pour les ondes de choc.
    static let ring: SKTexture = makeRing(diameter: 128, lineWidth: 6)

    private static func makeSoftDot(diameter: CGFloat) -> SKTexture {
        let size = CGSize(width: diameter, height: diameter)
        let image = UIGraphicsImageRenderer(size: size).image { ctx in
            let cg = ctx.cgContext
            let center = CGPoint(x: diameter / 2, y: diameter / 2)
            let colors = [
                UIColor(white: 1, alpha: 1).cgColor,
                UIColor(white: 1, alpha: 0.85).cgColor,
                UIColor(white: 1, alpha: 0).cgColor
            ] as CFArray
            guard let gradient = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(),
                                            colors: colors,
                                            locations: [0, 0.55, 1]) else { return }
            cg.drawRadialGradient(gradient,
                                  startCenter: center, startRadius: 0,
                                  endCenter: center, endRadius: diameter / 2,
                                  options: [])
        }
        let texture = SKTexture(image: image)
        texture.filteringMode = .linear
        return texture
    }

    private static func makeRing(diameter: CGFloat, lineWidth: CGFloat) -> SKTexture {
        let size = CGSize(width: diameter, height: diameter)
        let image = UIGraphicsImageRenderer(size: size).image { ctx in
            let cg = ctx.cgContext
            let inset = lineWidth / 2
            let rect = CGRect(x: inset, y: inset,
                              width: diameter - lineWidth, height: diameter - lineWidth)
            cg.setStrokeColor(UIColor.white.cgColor)
            cg.setLineWidth(lineWidth)
            cg.strokeEllipse(in: rect)
        }
        let texture = SKTexture(image: image)
        texture.filteringMode = .linear
        return texture
    }
}
