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
        center.removePendingNotificationRequests(withIdentifiers: [reminderID])

        let content = UNMutableNotificationContent()
        content.title = String(localized: "notif.daily_title", defaultValue: "Une pause zen ?")
        content.body = String(localized: "notif.daily_body", defaultValue: "Une grille t'attend 🌿")
        content.sound = .default

        var components = DateComponents()
        components.hour = reminderHour
        components.minute = 0
        let trigger = UNCalendarNotificationTrigger(dateMatching: components, repeats: true)

        let request = UNNotificationRequest(identifier: reminderID, content: content, trigger: trigger)
        center.add(request)
    }
}
