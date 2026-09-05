//
//  DuelService.swift
//  tenGO
//
//  Duel asynchrone (issue #23) : deux joueurs, la MÊME grille, deux scores
//  comparés. Aucun temps réel — le duel peut s'étaler sur plusieurs jours.
//
//  Le duel se partage par un CODE, pas par une liste d'amis : #22 n'existe
//  pas encore, et surtout un code se colle dans n'importe quelle messagerie,
//  sans obliger les deux joueurs à se trouver dans le jeu au préalable.
//
//  Ce qui rend le duel possible sans envoyer la grille : elle est entièrement
//  déterminée par sa graine (SeededGenerator), déjà éprouvée par le Défi du
//  jour. On ne transmet donc qu'un entier.
//

import Foundation
import FirebaseFirestore
import GameKit

struct Duel {
    let code: String
    let seed: UInt64
    let challengerUid: String
    let challengerName: String
    let challengerScore: Int
    let opponentUid: String?
    let opponentName: String?
    let opponentScore: Int?
    let expiresAt: Date

    var isComplete: Bool { opponentScore != nil }
    var isExpired: Bool { Date() > expiresAt }

    enum Outcome { case win, loss, draw }

    /// Issue du duel du point de vue d'un joueur, une fois les deux scores
    /// connus.
    func outcome(for uid: String) -> Outcome? {
        guard let opponentScore, let opponentUid else { return nil }
        let (mine, theirs): (Int, Int)
        if uid == challengerUid { (mine, theirs) = (challengerScore, opponentScore) }
        else if uid == opponentUid { (mine, theirs) = (opponentScore, challengerScore) }
        else { return nil }
        if mine > theirs { return .win }
        if mine < theirs { return .loss }
        return .draw
    }

    /// Nom de l'adversaire du point de vue d'un joueur.
    func rivalName(for uid: String) -> String {
        uid == challengerUid ? (opponentName ?? "") : challengerName
    }
}

final class DuelService {

    static let shared = DuelService()
    private init() {}

    enum DuelError: LocalizedError {
        case notFound
        case expired
        case alreadyPlayed
        case ownDuel
        case backend(String)

        var errorDescription: String? {
            switch self {
            case .notFound:      return String(localized: "duel.error_not_found")
            case .expired:       return String(localized: "duel.error_expired")
            case .alreadyPlayed: return String(localized: "duel.error_already_played")
            case .ownDuel:       return String(localized: "duel.error_own_duel")
            case .backend(let m): return m
            }
        }
    }

    /// Durée de vie d'un duel : au-delà, il n'est plus jouable.
    static let lifetime: TimeInterval = 7 * 24 * 3600

    private var collection: CollectionReference { FirebaseService.shared.db.collection("duels") }

    // MARK: - Code de partage

    /// Alphabet sans caractères confondables (ni O/0, ni I/1) : un code se
    /// lit à voix haute et se recopie à la main.
    private static let alphabet = Array("ABCDEFGHJKLMNPQRSTUVWXYZ23456789")

    private static func makeCode() -> String {
        String((0..<6).map { _ in alphabet.randomElement()! })
    }

    /// Nom affiché à l'adversaire. Game Center le fournit quand le joueur y
    /// est connecté ; sinon on reste anonyme plutôt que d'inventer une identité.
    static var displayName: String {
        let name = GKLocalPlayer.local.isAuthenticated ? GKLocalPlayer.local.displayName : ""
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? String(localized: "duel.anonymous_player") : String(trimmed.prefix(40))
    }

    // MARK: - Cycle de vie d'un duel

    /// Crée un duel à partir d'une partie que le challenger vient de jouer.
    func createDuel(seed: UInt64, score: Int) async throws -> Duel {
        let uid = try await FirebaseService.shared.signInIfNeeded()
        let code = Self.makeCode()
        let expiry = Date().addingTimeInterval(Self.lifetime)
        let name = Self.displayName

        let data: [String: Any] = [
            "seed": Int(bitPattern: UInt(truncatingIfNeeded: seed)),
            "challengerUid": uid,
            "challengerName": name,
            "challengerScore": score,
            "opponentUid": NSNull(),
            "expiresAt": Timestamp(date: expiry),
            "createdAt": FieldValue.serverTimestamp(),
        ]
        do {
            try await collection.document(code).setData(data)
        } catch {
            throw DuelError.backend(error.localizedDescription)
        }
        DuelHistory.remember(code)
        return Duel(code: code, seed: seed, challengerUid: uid, challengerName: name,
                    challengerScore: score, opponentUid: nil, opponentName: nil,
                    opponentScore: nil, expiresAt: expiry)
    }

    /// Récupère un duel par son code, et vérifie qu'il est jouable.
    func fetch(code: String) async throws -> Duel {
        _ = try await FirebaseService.shared.signInIfNeeded()
        let normalized = code.uppercased().trimmingCharacters(in: .whitespacesAndNewlines)
        let snapshot: DocumentSnapshot
        do {
            snapshot = try await collection.document(normalized).getDocument()
        } catch {
            throw DuelError.backend(error.localizedDescription)
        }
        guard snapshot.exists, let duel = Self.decode(snapshot) else { throw DuelError.notFound }
        return duel
    }

    /// Enregistre le score de l'adversaire. Les règles Firestore garantissent
    /// qu'un duel déjà tranché ne peut pas être rejoué : la vérification côté
    /// client n'est là que pour un message clair, pas pour la sécurité.
    func submitOpponentScore(code: String, score: Int) async throws -> Duel {
        let uid = try await FirebaseService.shared.signInIfNeeded()
        let duel = try await fetch(code: code)
        guard !duel.isExpired else { throw DuelError.expired }
        guard duel.opponentScore == nil else { throw DuelError.alreadyPlayed }
        guard duel.challengerUid != uid else { throw DuelError.ownDuel }

        let name = Self.displayName
        do {
            try await collection.document(duel.code).updateData([
                "opponentUid": uid,
                "opponentName": name,
                "opponentScore": score,
            ])
        } catch {
            throw DuelError.backend(error.localizedDescription)
        }
        DuelHistory.remember(duel.code)
        return Duel(code: duel.code, seed: duel.seed, challengerUid: duel.challengerUid,
                    challengerName: duel.challengerName, challengerScore: duel.challengerScore,
                    opponentUid: uid, opponentName: name, opponentScore: score,
                    expiresAt: duel.expiresAt)
    }

    /// Duels auxquels ce joueur a pris part, du plus récent au plus ancien.
    /// L'historique est LOCAL : les règles interdisent d'énumérer la
    /// collection (le code est le secret), et une requête serveur imposerait
    /// un index composite pour un besoin que le local couvre très bien.
    func myDuels() async -> [Duel] {
        var result: [Duel] = []
        for code in DuelHistory.codes {
            if let duel = try? await fetch(code: code) { result.append(duel) }
        }
        return result
    }

    private static func decode(_ snapshot: DocumentSnapshot) -> Duel? {
        guard let data = snapshot.data(),
              let rawSeed = data["seed"] as? Int,
              let challengerUid = data["challengerUid"] as? String,
              let challengerScore = data["challengerScore"] as? Int,
              let expires = data["expiresAt"] as? Timestamp else { return nil }
        return Duel(
            code: snapshot.documentID,
            seed: UInt64(bitPattern: Int64(rawSeed)),
            challengerUid: challengerUid,
            challengerName: data["challengerName"] as? String ?? "",
            challengerScore: challengerScore,
            opponentUid: data["opponentUid"] as? String,
            opponentName: data["opponentName"] as? String,
            opponentScore: data["opponentScore"] as? Int,
            expiresAt: expires.dateValue()
        )
    }
}

/// Codes des duels auxquels ce joueur a participé (local, cf. `myDuels`).
enum DuelHistory {

    private static let maximum = 20

    static var codes: [String] {
        UserDefaults.standard.stringArray(forKey: AppConfig.UserDefaultsKey.duelHistory) ?? []
    }

    static func remember(_ code: String) {
        var list = codes.filter { $0 != code }
        list.insert(code, at: 0)
        UserDefaults.standard.set(Array(list.prefix(maximum)), forKey: AppConfig.UserDefaultsKey.duelHistory)
    }
}
