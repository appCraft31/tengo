using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.Android;

namespace TenGo.Editor
{
    /// Liens profonds Android, posés sur l'activité de lancement du projet
    /// Gradle généré (le manifeste de l'app vient d'Unity) :
    /// - `tengo://…` (duel/CODE, daily, progress), comme AndroidManifest.xml 3.3.0 ;
    /// - `https://appcraft31.app/tengo/…`, lien universel (vérification
    ///   assetlinks.json du domaine : sinon le lien s'ouvre dans le navigateur).
    /// Le reste du manifeste (permissions, id Play Games) vient du module
    /// `Plugins/Android/TenGo.androidlib` ; l'id AdMob, du plugin Google Mobile Ads.
    public sealed class AndroidPostprocess : IPostGenerateGradleAndroidProject
    {
        const string Ns = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var activity = doc.SelectNodes("/manifest/application/activity").Cast<XmlElement>()
                .FirstOrDefault(a => a.SelectNodes("intent-filter/category").Cast<XmlElement>()
                    .Any(c => c.GetAttribute("name", Ns) == "android.intent.category.LAUNCHER"));
            if (activity == null || activity.InnerXml.Contains("\"tengo\"")) return;
            AddFilter(doc, activity, false, ("scheme", "tengo"));
            AddFilter(doc, activity, true, ("scheme", "https"), ("host", "appcraft31.app"), ("pathPrefix", "/tengo"));
            doc.Save(manifestPath);
        }

        public static void AddFilter(XmlDocument doc, XmlElement activity, bool autoVerify, params (string attr, string value)[] data)
        {
            var filter = doc.CreateElement("intent-filter");
            filter.SetAttribute("autoVerify", Ns, autoVerify ? "true" : "false");
            void Child(string name, string value)
            {
                var e = doc.CreateElement(name);
                e.SetAttribute("name", Ns, value);
                filter.AppendChild(e);
            }
            Child("action", "android.intent.action.VIEW");
            Child("category", "android.intent.category.DEFAULT");
            Child("category", "android.intent.category.BROWSABLE");
            var d = doc.CreateElement("data");
            foreach (var (attr, value) in data) d.SetAttribute(attr, Ns, value);
            filter.AppendChild(d);
            activity.AppendChild(filter);
        }
    }
}
