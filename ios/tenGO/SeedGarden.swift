//
//  SeedGarden.swift
//  tenGO
//
//  Graines de dix : la plus longue chaîne d'une partie plante une graine.
//  Quelques heures plus tard elle éclot en « fleur de dix », une bulle qui
//  multiplie les points de la chaîne qui la contient, posée dans la partie
//  suivante.
//
//  Pourquoi : la session se termine aujourd'hui sur un score, et rien ne
//  reste en cours. La graine laisse au joueur quelque chose qu'il a lui-même
//  provoqué en jouant bien, et qui l'attend plus tard. La règle du jeu ne
//  change pas : la fleur garde son chiffre et compte dans la somme comme les
//  autres ; elle récompense seulement la chaîne qui sait l'intégrer.
//
//  Un seul emplacement, pour que le rendez-vous reste lisible. Tout est
//  local (UserDefaults).
//

import Foundation

final class SeedGarden {

    static let shared = SeedGarden()
    private init() { load() }

    struct Seed: Codable, Equatable {
        let plantedAt: Date
        var chain: Int
        let bloomAt: Date

        var power: Int { SeedGarden.power(forChain: chain) }
    }

    enum Status: Equatable {
        case empty
        case growing(Seed)
        case bloomed(Seed)
    }

    /// Ce que la fin de partie dit de la graine.
    enum PlantOutcome: Equatable {
        /// Nouvelle graine plantée.
        case planted(power: Int, bloomAt: Date)
        /// Une graine poussait déjà : la chaîne plus longue la renforce.
        case strengthened(power: Int, bloomAt: Date)
        /// Une graine pousse déjà, au moins aussi forte.
        case stillGrowing(bloomAt: Date)
        /// Une fleur attend d'être utilisée.
        case flowerWaiting(power: Int)
        /// Chaîne trop courte pour planter.
        case tooShort(minimum: Int)
    }

    /// Longueur de chaîne minimale pour planter.
    static let minimumChain = 4
    /// Temps de pousse : assez long pour que la fleur se retrouve « plus
    /// tard », assez court pour éclore dans la journée.
    static let growth: TimeInterval = 4 * 3600

    /// Multiplicateur de la fleur selon la chaîne qui a planté la graine.
    static func power(forChain chain: Int) -> Int {
        switch chain {
        case ..<minimumChain: return 1
        case 4:               return 2
        case 5, 6:            return 3
        default:              return 4
        }
    }

    private let defaults = UserDefaults.standard
    private(set) var seed: Seed?

    private func load() {
        guard let data = defaults.data(forKey: AppConfig.UserDefaultsKey.seedGarden) else { return }
        seed = try? JSONDecoder().decode(Seed.self, from: data)
    }

    private func save() {
        if let seed, let data = try? JSONEncoder().encode(seed) {
            defaults.set(data, forKey: AppConfig.UserDefaultsKey.seedGarden)
        } else {
            defaults.removeObject(forKey: AppConfig.UserDefaultsKey.seedGarden)
        }
    }

    func status(now: Date = Date()) -> Status {
        guard let seed else { return .empty }
        return now >= seed.bloomAt ? .bloomed(seed) : .growing(seed)
    }

    /// Propose la plus longue chaîne de la partie qui vient de finir.
    func offer(chain: Int, now: Date = Date()) -> PlantOutcome {
        switch status(now: now) {
        case .bloomed(let existing):
            return .flowerWaiting(power: existing.power)
        case .growing(var existing):
            if Self.power(forChain: chain) > existing.power {
                existing.chain = chain
                seed = existing
                save()
                AnalyticsService.seedPlanted(power: existing.power, strengthened: true)
                return .strengthened(power: existing.power, bloomAt: existing.bloomAt)
            }
            return .stillGrowing(bloomAt: existing.bloomAt)
        case .empty:
            guard chain >= Self.minimumChain else { return .tooShort(minimum: Self.minimumChain) }
            let planted = Seed(plantedAt: now, chain: chain, bloomAt: now.addingTimeInterval(Self.growth))
            seed = planted
            save()
            NotificationManager.shared.scheduleSeedBloom(at: planted.bloomAt)
            AnalyticsService.seedPlanted(power: planted.power, strengthened: false)
            return .planted(power: planted.power, bloomAt: planted.bloomAt)
        }
    }

    /// Prend la fleur éclose pour la poser dans une nouvelle partie. Renvoie
    /// son multiplicateur, ou `nil` s'il n'y en a pas.
    func takeFlower(now: Date = Date()) -> Int? {
        guard case .bloomed(let flower) = status(now: now) else { return nil }
        seed = nil
        save()
        NotificationManager.shared.cancelSeedBloom()
        return flower.power
    }

    /// La toute première fleur posée est expliquée au joueur une seule fois.
    var flowerExplained: Bool {
        get { defaults.bool(forKey: AppConfig.UserDefaultsKey.seedFlowerExplained) }
        set { defaults.set(newValue, forKey: AppConfig.UserDefaultsKey.seedFlowerExplained) }
    }

    /// Durée restante lisible : « 3 h 20 », « 12 min ».
    static func remainingText(until date: Date, now: Date = Date()) -> String {
        let seconds = max(60, date.timeIntervalSince(now))
        let formatter = DateComponentsFormatter()
        formatter.unitsStyle = .abbreviated
        formatter.allowedUnits = seconds >= 3600 ? [.hour, .minute] : [.minute]
        formatter.maximumUnitCount = 2
        return formatter.string(from: seconds) ?? ""
    }

    #if DEBUG
    /// QA : fait éclore la graine tout de suite (ou en plante une éclose).
    func forceBloomForQA(chain: Int = 6) {
        let now = Date()
        seed = Seed(plantedAt: now.addingTimeInterval(-Self.growth), chain: chain, bloomAt: now.addingTimeInterval(-1))
        save()
    }

    func clearForQA() {
        seed = nil
        save()
    }
    #endif
}
