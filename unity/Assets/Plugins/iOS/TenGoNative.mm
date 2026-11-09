// Ponts natifs iOS de tenGO (Unity) : suivi publicitaire (ATT), feuille de
// partage, réglages de l'app, canal TestFlight, mode debug de Firebase.
// Appelé par Scripts/Services/Native/NativeBridge.cs. Compilé avec ARC.
#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

extern UIViewController *UnityGetGLViewController(void);

typedef void (*TenGoStatusCallback)(int status);

extern "C" {

/// ConsentManager.swift : requestTrackingAuthorization depuis le fil principal,
/// fenêtre active (sinon le statut reste « non déterminé »). Rappelle avec le
/// statut ATT (3 = autorisé). Un statut déjà tranché est rendu tel quel.
void tengo_request_tracking(TenGoStatusCallback callback) {
    if (@available(iOS 14, *)) {
        dispatch_async(dispatch_get_main_queue(), ^{
            ATTrackingManagerAuthorizationStatus current = ATTrackingManager.trackingAuthorizationStatus;
            if (current != ATTrackingManagerAuthorizationStatusNotDetermined) {
                if (callback) callback((int)current);
                return;
            }
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                dispatch_async(dispatch_get_main_queue(), ^{ if (callback) callback((int)status); });
            }];
        });
    } else {
        if (callback) callback(3);
    }
}

/// DuelShare.present / partage de l'app : UIActivityViewController ancrée au
/// centre de la vue (obligatoire sur iPad, sinon plantage).
void tengo_share_text(const char *text) {
    NSString *message = text ? [NSString stringWithUTF8String:text] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *root = UnityGetGLViewController();
        if (root == nil) return;
        UIActivityViewController *sheet = [[UIActivityViewController alloc] initWithActivityItems:@[message] applicationActivities:nil];
        sheet.popoverPresentationController.sourceView = root.view;
        sheet.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 0, 0);
        [root presentViewController:sheet animated:YES completion:nil];
    });
}

/// SettingsOverlay.openAppSettings : page de l'app dans Réglages (suivi, notifications).
void tengo_open_app_settings(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL *url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
        if (url) [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
    });
}

/// Build installée par TestFlight (ou Xcode) : reçu « sandboxReceipt ».
bool tengo_is_testflight(void) {
    NSURL *receipt = NSBundle.mainBundle.appStoreReceiptURL;
    return receipt != nil && [receipt.lastPathComponent isEqualToString:@"sandboxReceipt"];
}

/// Mode debug de Firebase Analytics (= -FIRAnalyticsDebugEnabled), réglé avant
/// l'initialisation et réécrit à chaque lancement (comme Binero).
void tengo_firebase_debug(bool enabled) {
    NSUserDefaults *d = NSUserDefaults.standardUserDefaults;
    for (NSString *key in @[@"/google/measurement/debug_mode", @"/google/firebase/debug_mode"]) {
        if (enabled) [d setBool:YES forKey:key];
        else [d removeObjectForKey:key];
    }
}

}
