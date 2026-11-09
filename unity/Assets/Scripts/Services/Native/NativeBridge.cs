using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace TenGo.Services
{
    /// Ponts natifs hors SDK : suivi publicitaire (ATT), feuille de partage,
    /// réglages de l'app, canal TestFlight, mode debug de Firebase.
    /// iOS : `Plugins/iOS/TenGoNative.mm`. Android : intents par JNI.
    /// Éditeur : sans effet (journal).
    public static class NativeBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        delegate void StatusCallback(int status);
        [DllImport("__Internal")] static extern void tengo_request_tracking(StatusCallback callback);
        [DllImport("__Internal")] static extern void tengo_share_text(string text);
        [DllImport("__Internal")] static extern void tengo_open_app_settings();
        [DllImport("__Internal")] static extern bool tengo_is_testflight();
        [DllImport("__Internal")] static extern void tengo_firebase_debug(bool enabled);

        static Action<bool> _trackingDone;

        [MonoPInvokeCallback(typeof(StatusCallback))]
        static void OnTracking(int status)
        {
            var done = _trackingDone;
            _trackingDone = null;
            // 3 = ATTrackingManagerAuthorizationStatusAuthorized.
            MainThread.Run(() => done?.Invoke(status == 3));
        }
#endif

        /// ConsentManager.swift : demande ATT sur le fil principal, fenêtre
        /// active ; seul « autorisé » vaut consentement. Hors iOS : faux.
        public static void RequestTracking(Action<bool> done)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _trackingDone = done;
            tengo_request_tracking(OnTracking);
#else
            done?.Invoke(false);
#endif
        }

        /// Feuille de partage du système avec un texte (UIActivityViewController
        /// ancrée au centre sur iPad, ou `Intent.ACTION_SEND` + sélecteur).
        public static void ShareText(string text)
        {
#if UNITY_IOS && !UNITY_EDITOR
            tengo_share_text(text);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, (string)null);
                Activity?.Call("startActivity", chooser);
            }
            catch (Exception e) { Debug.LogWarning("[Share] " + e.Message); }
#else
            Debug.Log("[Share] " + text);
#endif
        }

        /// Page de l'app dans les réglages système (suivi, notifications).
        public static void OpenAppSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            tengo_open_app_settings();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
                Activity?.Call("startActivity", intent);
            }
            catch (Exception e) { Debug.LogWarning("[Settings] " + e.Message); }
#else
            Debug.Log("[Settings] réglages système de l'app");
#endif
        }

        /// Build installée par TestFlight (reçu `sandboxReceipt`).
        public static bool IsTestFlight()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return tengo_is_testflight();
#else
            return false;
#endif
        }

        /// iOS : mode debug de Firebase Analytics (DebugView, exclu des rapports).
        public static void SetFirebaseDebug(bool enabled)
        {
#if UNITY_IOS && !UNITY_EDITOR
            tengo_firebase_debug(enabled);
#endif
        }

#if UNITY_ANDROID
        /// Activité Unity courante.
        public static AndroidJavaObject Activity
        {
            get
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
#endif

        /// Canal de la build (propriété `build_channel`, comme Binero) :
        /// « development » (build de développement), « testflight », « production ».
        public static string BuildChannel =>
            Debug.isDebugBuild ? "development" : IsTestFlight() ? "testflight" : "production";
    }
}
