//
//  NotificationManager.swift
//  tenGO
//
//  Notifications locales, 100 % (UNUserNotificationCenter, aucun serveur).
//  La permission est demandée de façon différée — après quelques parties —
//  pour un meilleur taux d'acceptation et une approche moins intrusive.
//
//  Peu, mais intelligentes (GDD §46) : chaque notification est reconstruite
//  avec les vraies données du joueur (série en cours) et reprogrammée à
//  chaque événement pertinent — jamais un rappel générique style
//  « Reviens jouer ! » qui ne dit rien de la partie du joueur.
//

import Foundation
import UserNotifications

final class NotificationManager {

    static let shared = NotificationManager()
    private init() {}

    private let center = UNUserNotificationCenter.current()
    private let defaults = UserDefaults.standard
    private let dailyAvailableID = "tenGO.dailyAvailable"
    private let streakAtRiskID = "tenGO.streakAtRisk"

    /// Nombre de parties avant de proposer les notifications.
    private let promptThreshold = 3
    /// Heure (locale) de la notification « défi disponible » — le défi est dispo
    /// dès 5h, mais on notifie à une heure plus douce.
    private let dailyAvailableHour = 9
    /// Heure (locale) du rappel de série à risque — laisse une marge
    /// confortable avant la bascule du jour (5h le lendemain).
    private let streakAtRiskHour = 20

    // MARK: - Cycle de vie

    /// À appeler au lancement d'une partie. Propose les notifications une fois
    /// le seuil atteint (et une seule fois), puis reprogramme le rappel de
    /// série (la partie qui vient d'être jouée a pu la sécuriser pour la
    /// journée — plus rien à rappeler).
    func registerGameAndMaybeRequest() {
        let count = defaults.integer(forKey: AppConfig.UserDefaultsKey.notifGamesPlayed) + 1
        defaults.set(count, forKey: AppConfig.UserDefaultsKey.notifGamesPlayed)

        let alreadyAsked = defaults.bool(forKey: AppConfig.UserDefaultsKey.notifRequested)
        if count >= promptThreshold && !alreadyAsked {
            requestAuthorization()
        } else {
            refreshIfAuthorized()
        }
    }

    /// Reprogramme les notifications à l'ouverture de l'app si autorisées.
    func refreshIfAuthorized() {
        center.getNotificationSettings { [weak self] settings in
            guard settings.authorizationStatus == .authorized else { return }
            self?.scheduleDailyAvailable()
            self?.rescheduleStreakAtRisk()
        }
    }

    // MARK: - Permission

    private func requestAuthorization() {
        defaults.set(true, forKey: AppConfig.UserDefaultsKey.notifRequested)
        center.requestAuthorization(options: [.alert, .sound, .badge]) { [weak self] granted, _ in
            guard granted else { return }
            self?.scheduleDailyAvailable()
            self?.rescheduleStreakAtRisk()
        }
    }

    // MARK: - Défi du jour disponible (répétée, contenu fixe — l'événement
    // lui-même ne dépend d'aucune donnée joueur).

    private func scheduleDailyAvailable() {
        let content = UNMutableNotificationContent()
        content.title = String(localized: "notif.daily_available_title", defaultValue: "Défi du jour 🧩")
        content.body = String(localized: "notif.daily_available_body", defaultValue: "Un nouveau défi t'attend !")
        content.sound = .default

        var components = DateComponents()
        components.hour = dailyAvailableHour
        components.minute = 0
        let trigger = UNCalendarNotificationTrigger(dateMatching: components, repeats: true)
        center.add(UNNotificationRequest(identifier: dailyAvailableID, content: content, trigger: trigger))
    }

    // MARK: - Série à risque (reprogrammée à chaque événement pertinent —
    // le contenu embarque la vraie série du joueur, donc pas de trigger
    // répétitif générique possible : un tir unique, refait à chaque fois).

    /// À appeler après toute partie jouée, et à chaque ouverture de l'app :
    /// si la série est active et pas encore sécurisée aujourd'hui, programme
    /// un rappel personnalisé ce soir ; sinon annule tout rappel en attente.
    func rescheduleStreakAtRisk() {
        center.removePendingNotificationRequests(withIdentifiers: [streakAtRiskID])

        let streak = StreakManager.shared.current
        guard streak > 0, !StreakManager.shared.hasPlayedToday() else { return }

        let calendar = Calendar.current
        guard let fireDate = calendar.date(bySettingHour: streakAtRiskHour, minute: 0, second: 0, of: Date()),
              fireDate > Date() else { return }

        let content = UNMutableNotificationContent()
        content.title = String(format: String(localized: "notif.streak_at_risk_title"), streak)
        content.body = String(localized: "notif.streak_at_risk_body")
        content.sound = .default

        let components = calendar.dateComponents([.year, .month, .day, .hour, .minute], from: fireDate)
        let trigger = UNCalendarNotificationTrigger(dateMatching: components, repeats: false)
        center.add(UNNotificationRequest(identifier: streakAtRiskID, content: content, trigger: trigger))
    }
}
