//
//  CognitiveMirror.swift
//  tenGO
//
//  Miroir cognitif : le jeu observe, sans rien demander, la façon dont le
//  joueur cherche ses 10, et lui montre ce qui progresse.
//
//  Constat de départ (audit du 30/10/2026) : des parties longues (20 min),
//  mais 11 nouveaux joueurs sur 100 seulement reviennent la semaine suivante.
//  La partie plaît ; rien ne dit au joueur qu'il s'améliore. Ce module mesure
//  quatre aptitudes, toutes tirées de gestes déjà faits :
//
//  - Coup d'œil   : temps entre le moment où la grille est jouable et le
//                   début du tracé qui sera validé. Plus court = mieux.
//  - Vision       : nombre moyen de bulles par chaîne validée.
//  - Anticipation : part des chaînes qui utilisent une bulle tombée au coup
//                   précédent — le joueur exploite ce que la chute a créé.
//  - Répertoire   : façons différentes de faire 10 déjà trouvées, parmi les
//                   41 décompositions possibles (1+9, 2+3+5, 1+1+…+1…).
//
//  Tout est local (UserDefaults), comme PlayerStatsManager.
//

import Foundation

// MARK: - Mesures d'une partie

/// Ce que la scène de jeu relève pendant une partie. Valeur pure, remise à
/// zéro à chaque nouvelle grille.
struct MirrorGameTally: Codable, Equatable {
    var moves = 0
    var lengthSum = 0
    var lookSum: Double = 0
    var lookCount = 0
    var fallChains = 0
    var fallEligible = 0
    /// Décompositions jouées dans la partie (clés canoniques, cf. `Partition`).
    var partitions: [String] = []

    /// Délais au-delà desquels le joueur fait autre chose que chercher (pause,
    /// téléphone posé) : la mesure serait faussée.
    static let maxLookSeconds: Double = 20

    mutating func record(values: [Int], lookSeconds: Double?, usedFallenBubble: Bool, previousMoveHadFalls: Bool) {
        moves += 1
        lengthSum += values.count
        if let look = lookSeconds, look >= 0, look <= Self.maxLookSeconds {
            lookSum += look
            lookCount += 1
        }
        if previousMoveHadFalls {
            fallEligible += 1
            if usedFallenBubble { fallChains += 1 }
        }
        let key = Partition.key(for: values)
        if !partitions.contains(key) { partitions.append(key) }
    }
}

// MARK: - Décompositions de 10

enum Partition {

    /// Clé canonique : valeurs triées, jointes par « + » (« 1+2+7 »).
    static func key(for values: [Int]) -> String {
        values.sorted().map(String.init).joined(separator: "+")
    }

    /// Les 41 façons d'écrire 10 avec au moins deux nombres de 1 à 9, rangées
    /// par nombre de termes puis dans l'ordre lexicographique.
    static let all: [[Int]] = {
        var result: [[Int]] = []
        func build(_ remaining: Int, _ maxPart: Int, _ current: [Int]) {
            if remaining == 0 {
                if current.count >= 2 { result.append(current.sorted()) }
                return
            }
            for part in stride(from: min(maxPart, remaining, 9), through: 1, by: -1) {
                build(remaining - part, part, current + [part])
            }
        }
        build(10, 9, [])
        return result.sorted {
            $0.count != $1.count ? $0.count < $1.count : $0.lexicographicallyPrecedes($1)
        }
    }()

    static var allKeys: [String] { all.map { key(for: $0) } }

    /// Texte affiché : « 2 + 3 + 5 ».
    static func display(_ key: String) -> String {
        key.replacingOccurrences(of: "+", with: " + ")
    }
}

// MARK: - Partie archivée

private struct MirrorGameRecord: Codable {
    let date: Date
    let moves: Int
    let lengthSum: Int
    let lookSum: Double
    let lookCount: Int
    let fallChains: Int
    let fallEligible: Int
}

// MARK: - Les quatre aptitudes

enum MirrorSkill: String, CaseIterable {
    case look, vision, anticipation, repertoire

    var titleKey: String { "mirror.skill.\(rawValue)" }
    var explainKey: String { "mirror.skill.\(rawValue).explain" }
}

/// Une aptitude sur une fenêtre de parties.
struct MirrorReading {
    let value: Double?
    let previous: Double?

    /// Progrès relatif dans le bon sens (positif = mieux), `nil` si la
    /// comparaison n'a pas de base.
    func improvement(lowerIsBetter: Bool) -> Double? {
        guard let value, let previous, previous > 0 else { return nil }
        let delta = (value - previous) / previous
        return lowerIsBetter ? -delta : delta
    }
}

// MARK: - Gestionnaire

final class CognitiveMirror {

    static let shared = CognitiveMirror()
    private init() { load() }

    private let defaults = UserDefaults.standard
    private var records: [MirrorGameRecord] = []
    /// Décomposition → date de découverte.
    private(set) var discovered: [String: Date] = [:]

    /// Parties archivées au maximum (environ deux mois de jeu régulier).
    private static let maxRecords = 120
    /// Coups minimum pour qu'une partie compte dans les moyennes.
    static let minMovesForReading = 4

    private func load() {
        if let data = defaults.data(forKey: AppConfig.UserDefaultsKey.mirrorRecords),
           let decoded = try? JSONDecoder().decode([MirrorGameRecord].self, from: data) {
            records = decoded
        }
        if let data = defaults.data(forKey: AppConfig.UserDefaultsKey.mirrorPartitions),
           let decoded = try? JSONDecoder().decode([String: Date].self, from: data) {
            discovered = decoded
        }
    }

    private func save() {
        if let data = try? JSONEncoder().encode(records) {
            defaults.set(data, forKey: AppConfig.UserDefaultsKey.mirrorRecords)
        }
        if let data = try? JSONEncoder().encode(discovered) {
            defaults.set(data, forKey: AppConfig.UserDefaultsKey.mirrorPartitions)
        }
    }

    var gamesMeasured: Int { records.count }
    var repertoireCount: Int { discovered.count }
    static var repertoireTotal: Int { Partition.all.count }

    // MARK: Lectures

    private func value(_ skill: MirrorSkill, over window: [MirrorGameRecord]) -> Double? {
        switch skill {
        case .look:
            let count = window.reduce(0) { $0 + $1.lookCount }
            guard count > 0 else { return nil }
            return window.reduce(0) { $0 + $1.lookSum } / Double(count)
        case .vision:
            let moves = window.reduce(0) { $0 + $1.moves }
            guard moves > 0 else { return nil }
            return Double(window.reduce(0) { $0 + $1.lengthSum }) / Double(moves)
        case .anticipation:
            let eligible = window.reduce(0) { $0 + $1.fallEligible }
            guard eligible > 0 else { return nil }
            return Double(window.reduce(0) { $0 + $1.fallChains }) / Double(eligible)
        case .repertoire:
            return Double(discovered.count)
        }
    }

    static func lowerIsBetter(_ skill: MirrorSkill) -> Bool { skill == .look }

    /// Lecture « cette semaine contre avant » pour l'écran du profil : les
    /// parties des 7 derniers jours face aux 30 précédentes. Faute de parties
    /// récentes, les 5 dernières parties tiennent lieu de « maintenant ».
    func weeklyReading(_ skill: MirrorSkill, now: Date = Date()) -> MirrorReading {
        let usable = records.filter { $0.moves >= Self.minMovesForReading }
        if skill == .repertoire {
            let weekAgo = now.addingTimeInterval(-7 * 86_400)
            let before = discovered.values.filter { $0 < weekAgo }.count
            return MirrorReading(value: Double(discovered.count),
                                 previous: before > 0 ? Double(before) : nil)
        }
        let weekAgo = now.addingTimeInterval(-7 * 86_400)
        var recent = usable.filter { $0.date >= weekAgo }
        var older = Array(usable.filter { $0.date < weekAgo }.suffix(30))
        if recent.isEmpty {
            recent = Array(usable.suffix(5))
            older = Array(usable.dropLast(5).suffix(30))
        }
        return MirrorReading(value: value(skill, over: recent),
                             previous: older.isEmpty ? nil : value(skill, over: older))
    }

    // MARK: Fin de partie

    /// Ce que l'écran de fin dit au joueur. Une seule phrase, la plus
    /// valorisante qui soit vraie.
    enum Insight: Equatable {
        case discovered(key: String, total: Int)
        case faster(seconds: Double, gain: Double)
        case longer(average: Double, usual: Double)
        case anticipation(percent: Int, usual: Int)
        case nextTarget(length: Int, found: Int, total: Int)

        var analyticsName: String {
            switch self {
            case .discovered:   return "discovered"
            case .faster:       return "look"
            case .longer:       return "vision"
            case .anticipation: return "anticipation"
            case .nextTarget:   return "target"
            }
        }
    }

    /// Archive la partie et choisit la phrase de fin. À appeler une fois par
    /// partie terminée (hors démo).
    @discardableResult
    func finishGame(_ tally: MirrorGameTally, now: Date = Date()) -> Insight? {
        guard tally.moves > 0 else { return nil }
        let history = records.filter { $0.moves >= Self.minMovesForReading }

        // Nouvelles décompositions : la découverte prime sur tout le reste.
        var fresh: [String] = []
        for key in tally.partitions where discovered[key] == nil {
            discovered[key] = now
            fresh.append(key)
        }

        let record = MirrorGameRecord(date: now, moves: tally.moves, lengthSum: tally.lengthSum,
                                      lookSum: tally.lookSum, lookCount: tally.lookCount,
                                      fallChains: tally.fallChains, fallEligible: tally.fallEligible)
        records.append(record)
        if records.count > Self.maxRecords { records.removeFirst(records.count - Self.maxRecords) }
        save()

        if let rarest = fresh.max(by: { $0.split(separator: "+").count < $1.split(separator: "+").count }) {
            return .discovered(key: rarest, total: discovered.count)
        }
        return progressInsight(tally: tally, history: Array(history.suffix(30))) ?? nextTargetInsight()
    }

    /// Meilleur progrès de la partie face à la moyenne des parties passées.
    /// Il faut au moins trois parties de référence et quatre coups joués,
    /// sinon l'écart ne veut rien dire.
    private func progressInsight(tally: MirrorGameTally, history: [MirrorGameRecord]) -> Insight? {
        guard tally.moves >= Self.minMovesForReading, history.count >= 3 else { return nil }
        var candidates: [(gain: Double, insight: Insight)] = []

        if tally.lookCount >= 3, let usual = value(.look, over: history), usual > 0 {
            let current = tally.lookSum / Double(tally.lookCount)
            let gain = (usual - current) / usual
            if gain >= 0.10 && usual - current >= 0.2 {
                candidates.append((gain, .faster(seconds: current, gain: usual - current)))
            }
        }
        if let usual = value(.vision, over: history), usual > 0 {
            let current = Double(tally.lengthSum) / Double(tally.moves)
            let gain = (current - usual) / usual
            if gain >= 0.10 {
                candidates.append((gain, .longer(average: current, usual: usual)))
            }
        }
        if tally.fallEligible >= 3, let usual = value(.anticipation, over: history) {
            let current = Double(tally.fallChains) / Double(tally.fallEligible)
            if current - usual >= 0.10 {
                candidates.append((current - usual, .anticipation(percent: Int((current * 100).rounded()),
                                                                  usual: Int((usual * 100).rounded()))))
            }
        }
        return candidates.max(by: { $0.gain < $1.gain })?.insight
    }

    /// À défaut de progrès mesurable : la plus petite famille de
    /// décompositions encore incomplète, comme cible pour la prochaine partie.
    private func nextTargetInsight() -> Insight? {
        let missing = Partition.all.filter { discovered[Partition.key(for: $0)] == nil }
        guard let shortest = missing.min(by: { $0.count < $1.count }) else { return nil }
        return .nextTarget(length: shortest.count, found: discovered.count, total: Self.repertoireTotal)
    }

    /// Phrase localisée de l'écran de fin.
    static func text(for insight: Insight) -> String {
        switch insight {
        case let .discovered(key, total):
            return String(format: String(localized: "mirror.insight.discovered"),
                          Partition.display(key), total, repertoireTotal)
        case let .faster(seconds, gain):
            return String(format: String(localized: "mirror.insight.faster"),
                          formatSeconds(seconds), formatSeconds(gain))
        case let .longer(average, usual):
            return String(format: String(localized: "mirror.insight.longer"),
                          formatDecimal(average), formatDecimal(usual))
        case let .anticipation(percent, usual):
            return String(format: String(localized: "mirror.insight.anticipation"), percent, usual)
        case let .nextTarget(length, found, total):
            return String(format: String(localized: "mirror.insight.target"), found, total, length)
        }
    }

    static func formatSeconds(_ value: Double) -> String {
        formatDecimal(value)
    }

    static func formatDecimal(_ value: Double) -> String {
        let formatter = NumberFormatter()
        formatter.minimumFractionDigits = 1
        formatter.maximumFractionDigits = 1
        return formatter.string(from: NSNumber(value: value)) ?? String(format: "%.1f", value)
    }

    /// Valeur affichée d'une aptitude sur l'écran « Ton cerveau ».
    static func displayValue(_ skill: MirrorSkill, _ value: Double?) -> String {
        guard let value else { return "—" }
        switch skill {
        case .look:         return String(format: String(localized: "mirror.unit.seconds"), formatDecimal(value))
        case .vision:       return formatDecimal(value)
        case .anticipation: return "\(Int((value * 100).rounded())) %"
        case .repertoire:   return "\(Int(value)) / \(repertoireTotal)"
        }
    }

    #if DEBUG
    /// QA : remplit un historique crédible pour l'écran « Ton cerveau ».
    func seedForQA() {
        let now = Date()
        records = (0..<12).map { i in
            let daysAgo = Double(14 - i)
            let t = Double(i)
            return MirrorGameRecord(date: now.addingTimeInterval(-daysAgo * 86_400), moves: 18,
                                    lengthSum: Int(18 * (2.6 + t * 0.06)), lookSum: 18 * (3.2 - t * 0.12),
                                    lookCount: 18, fallChains: 4 + i / 2, fallEligible: 17)
        }
        for (i, key) in Partition.allKeys.prefix(17).enumerated() {
            discovered[key] = now.addingTimeInterval(-Double(20 - i) * 86_400)
        }
        save()
    }
    #endif
}
