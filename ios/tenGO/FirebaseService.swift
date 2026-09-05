//
//  FirebaseService.swift
//  tenGO
//
//  Socle des fonctionnalités en ligne (Duel, Amis, Leagues) : identité du
//  joueur et accès Firestore.
//
//  Deux partis pris :
//
//  1. Connexion ANONYME et PARESSEUSE. Anonyme parce que le jeu n'a jamais
//     demandé de compte et que ça doit le rester ; paresseuse parce qu'un
//     joueur qui ne touche jamais au Duel n'a aucune raison d'exister dans
//     une base. On ne crée donc un compte qu'au premier usage réel.
//
//  2. Dégradation silencieuse. Le hors-ligne est l'état normal d'un jeu
//     mobile : rien de ce qui est en ligne ne doit empêcher de jouer. Toute
//     erreur remonte à l'appelant, qui affiche un message et rend la main —
//     jamais de blocage ni de plantage.
//
//  ⚠️ Limite connue de l'identité anonyme : le compte est lié à
//  l'installation. Désinstaller l'app perd l'identité, donc les duels en
//  cours. Le rattachement à Sign in with Apple (Auth.linkWithCredential)
//  reste possible sans migration : l'uid est conservé.
//

import Foundation
import FirebaseAuth
import FirebaseFirestore

final class FirebaseService {

    static let shared = FirebaseService()
    private init() {}

    enum ServiceError: LocalizedError {
        /// Le fournisseur anonyme n'est pas activé côté console Firebase.
        case authNotConfigured
        case signInFailed(String)
        case offline

        var errorDescription: String? {
            switch self {
            case .authNotConfigured:
                return "L'authentification anonyme n'est pas activée sur le projet Firebase."
            case .signInFailed(let reason):
                return "Connexion impossible : \(reason)"
            case .offline:
                return "Aucune connexion réseau."
            }
        }
    }

    /// Identifiant du joueur, s'il est déjà connecté.
    var uid: String? { Auth.auth().currentUser?.uid }

    var isSignedIn: Bool { uid != nil }

    var db: Firestore { Firestore.firestore() }

    /// Connecte le joueur si besoin et retourne son identifiant.
    /// Idempotent : ne recrée jamais de compte si une session existe déjà.
    @discardableResult
    func signInIfNeeded() async throws -> String {
        if let uid { return uid }
        do {
            let result = try await Auth.auth().signInAnonymously()
            return result.user.uid
        } catch let error as NSError {
            throw Self.mapped(error)
        }
    }

    /// Traduit les erreurs Firebase en cas explicites — surtout
    /// `CONFIGURATION_NOT_FOUND`, qui signale une console non configurée et
    /// non un problème du joueur : sans ça on afficherait « réessayez » à
    /// quelqu'un dont rien ne peut marcher.
    private static func mapped(_ error: NSError) -> ServiceError {
        // Le SDK masque les erreurs serveur derrière « An internal error has
        // occurred » : le vrai code est dans la réponse désérialisée, jointe
        // au userInfo. Sans creuser jusque-là, un projet non configuré est
        // indiscernable d'une panne réseau.
        var haystack = [error.localizedDescription]
        if let response = error.userInfo["FIRAuthErrorUserInfoDeserializedResponseKey"] {
            haystack.append(String(describing: response))
        }
        if let underlying = error.userInfo[NSUnderlyingErrorKey] as? NSError {
            haystack.append(underlying.localizedDescription)
            if let response = underlying.userInfo["FIRAuthErrorUserInfoDeserializedResponseKey"] {
                haystack.append(String(describing: response))
            }
        }
        let details = haystack.joined(separator: " | ")

        if details.contains("CONFIGURATION_NOT_FOUND") || details.contains("ADMIN_ONLY_OPERATION")
            || details.contains("OPERATION_NOT_ALLOWED") {
            return .authNotConfigured
        }
        if error.domain == NSURLErrorDomain
            || AuthErrorCode(rawValue: error.code) == .networkError {
            return .offline
        }
        return .signInFailed(details)
    }

    /// Diagnostic de bout en bout : connexion, écriture puis relecture d'un
    /// document jetable. Sert à valider l'installation (règles comprises)
    /// sans dépendre d'une fonctionnalité de jeu.
    func runConnectivityCheck() async -> String {
        do {
            let uid = try await signInIfNeeded()
            let document = db.collection("players").document(uid)
            try await document.setData(["name": "diagnostic"], merge: true)
            let snapshot = try await document.getDocument()
            let name = snapshot.data()?["name"] as? String ?? "(vide)"
            return "OK — uid \(uid.prefix(8))…, relecture « \(name) »"
        } catch let error as ServiceError {
            return "ÉCHEC — \(error.localizedDescription)"
        } catch {
            return "ÉCHEC — \(error.localizedDescription)"
        }
    }
}
