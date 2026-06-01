//
//  NotificationManager.swift
//  tenGO
//
//  Rappels quotidiens 100 % locaux (UNUserNotificationCenter, aucun serveur).
//  La permission est demandée de façon différée — après quelques parties — pour
//  un meilleur taux d'acceptation et une approche moins intrusive.
//

import Foundation
import UserNotifications

final class NotificationManager {

    static let shared = NotificationManager()
    private init() {}

    private let center = UNUserNotificationCenter.current()
    private let defaults = UserDefaults.standard
    private let reminderID = "tenGO.dailyReminder"
    private let dailyAvailableID = "tenGO.dailyAvailable"

    /// Nombre de parties avant de proposer les notifications.
    private let promptThreshold = 3
    /// Heure (locale) du rappel quotidien.
    private let reminderHour = 19

    // MARK: - Cycle de vie

    /// À appeler au lancement d'une partie. Propose les notifications une fois
    /// le seuil atteint (et une seule fois).
    func registerGameAndMaybeRequest() {
        let count = defaults.integer(forKey: AppConfig.UserDefaultsKey.notifGamesPlayed) + 1
        defaults.set(count, forKey: AppConfig.UserDefaultsKey.notifGamesPlayed)

        let alreadyAsked = defaults.bool(forKey: AppConfig.UserDefaultsKey.notifRequested)
        if count >= promptThreshold && !alreadyAsked {
            requestAuthorization()
        }
    }

    /// Reprogramme le rappel à l'ouverture de l'app si l'utilisateur a autorisé.
    func refreshIfAuthorized() {
        center.getNotificationSettings { [weak self] settings in
            guard settings.authorizationStatus == .authorized else { return }
            self?.scheduleDailyReminder()
        }
    }

    // MARK: - Permission

    private func requestAuthorization() {
        defaults.set(true, forKey: AppConfig.UserDefaultsKey.notifRequested)
        center.requestAuthorization(options: [.alert, .sound, .badge]) { [weak self] granted, _ in
            guard granted else { return }
            self?.scheduleDailyReminder()
        }
    }

    // MARK: - Planification

    private func scheduleDailyReminder() {
        center.removePendingNotificationRequests(withIdentifiers: [reminderID, dailyAvailableID])

        // Rappel du soir — invite à revenir jouer (pause zen).
        scheduleRepeating(
            id: reminderID,
            hour: reminderHour,
            title: String(localized: "notif.daily_title", defaultValue: "Une pause zen ?"),
            body: String(localized: "notif.daily_body", defaultValue: "Une grille t'attend 🌿")
        )

        // Matin (5h) — un nouveau Défi du jour est disponible.
        scheduleRepeating(
            id: dailyAvailableID,
            hour: DailyChallenge.resetHour,
            title: String(localized: "notif.daily_available_title", defaultValue: "Défi du jour 🧩"),
            body: String(localized: "notif.daily_available_body", defaultValue: "Un nouveau défi t'attend !")
        )
    }

    /// Planifie une notification locale quotidienne répétée à l'heure indiquée.
    private func scheduleRepeating(id: String, hour: Int, title: String, body: String) {
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        content.sound = .default

        var components = DateComponents()
        components.hour = hour
        components.minute = 0
        let trigger = UNCalendarNotificationTrigger(dateMatching: components, repeats: true)

        center.add(UNNotificationRequest(identifier: id, content: content, trigger: trigger))
    }
}
