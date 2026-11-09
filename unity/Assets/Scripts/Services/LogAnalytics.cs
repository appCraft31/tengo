using System.Collections.Generic;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Services
{
    /// Analytics simulées (éditeur, tests, pilote automatique) : journal
    /// `[Analytics] nom k=v…` et mémoire des événements et des propriétés,
    /// avec exactement les noms et paramètres envoyés à Firebase.
    public sealed class LogAnalytics : EventAnalytics
    {
        public readonly List<string> Events = new();
        public readonly List<AnalyticsEvent> Sent = new();
        public readonly Dictionary<string, string> UserProperties = new();
        public bool Echo = true;

        protected override void Send(AnalyticsEvent e)
        {
            Sent.Add(e);
            string line = e.ToString();
            Events.Add(line);
            if (Echo) Debug.Log("[Analytics] " + line);
        }

        public override void SetUserProperty(string name, string value)
        {
            UserProperties[name] = value;
            if (Echo) Debug.Log("[Analytics] propriété " + name + "=" + value);
        }

        public int Count(string name) => Events.FindAll(e => e == name || e.StartsWith(name + " ")).Count;
    }
}
