#nullable enable
using System;

namespace TenGo.Core
{
    /// Lien entrant analysé : destination et, pour un duel, le code (majuscules).
    public readonly struct DeepLink
    {
        public readonly DeepLinkKind Kind;
        public readonly string? DuelCode;

        public DeepLink(DeepLinkKind kind, string? duelCode = null)
        {
            Kind = kind;
            DuelCode = duelCode;
        }

        public bool IsRecognized => Kind != DeepLinkKind.None;

        public static readonly DeepLink Unrecognized = new(DeepLinkKind.None);

        /// Analyse une URL entrante (DeepLink.swift `handle`), schéma
        /// personnalisé (`tengo://daily`) comme lien universel
        /// (`https://appcraft31.app/tengo/daily`). Le préfixe `/tengo` du site
        /// est retiré avant de router, pour que les deux formes empruntent les
        /// mêmes branches. Le code d'un duel se lit dans le chemin, jamais dans
        /// l'hôte (qui vaut « appcraft31.app » en lien universel).
        public static DeepLink Parse(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return Unrecognized;
            if (!Uri.TryCreate(url!.Trim(), UriKind.Absolute, out var uri)) return Unrecognized;
            string host = (uri.Host ?? "").ToLowerInvariant();
            string path = Uri.UnescapeDataString(uri.AbsolutePath ?? "").ToLowerInvariant();
            // `url.path` de Foundation : vide pour `tengo://daily`, sans barre finale.
            if (path.Length > 1 && path.EndsWith("/")) path = path.TrimEnd('/');
            if (path == "/") path = "";

            if (path == "/tengo") path = "/";
            else if (path.StartsWith("/tengo/")) path = path.Substring("/tengo".Length);

            if (host == "duel" || path == "/duel" || path.StartsWith("/duel/"))
            {
                string? code = null;
                foreach (var segment in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (segment == "duel") continue;
                    code = segment;
                    break;
                }
                if (string.IsNullOrEmpty(code)) return Unrecognized;
                return new DeepLink(DeepLinkKind.Duel, code!.ToUpperInvariant());
            }

            if (host == "event" || path == "/event" || host == "progress" || path == "/progress")
                return new DeepLink(DeepLinkKind.Progress);

            if (host == "daily" || path == "/daily" || path.StartsWith("/daily/")) return new DeepLink(DeepLinkKind.Daily);
            return Unrecognized;
        }
    }
}
