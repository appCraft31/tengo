using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Lecture des icônes SVG du catalogue iOS (traits seuls) : éléments
    /// `path` (M L H V C S Q A Z, absolus et relatifs), `polyline`, `line`.
    /// Les courbes et arcs sont aplatis en polylignes, dans le repère du
    /// `viewBox` (y vers le bas, comme le SVG).
    public static class SvgPath
    {
        const int CurveSteps = 16;

        /// Polylignes de tous les éléments d'un fragment SVG.
        public static List<List<Vector2>> ParseElements(string svg)
        {
            var result = new List<List<Vector2>>();
            foreach (Match m in Regex.Matches(svg, @"<(path|polyline|line)\b([^>]*)/?>"))
            {
                var attrs = m.Groups[2].Value;
                switch (m.Groups[1].Value)
                {
                    case "path":
                        result.AddRange(ParsePath(Attr(attrs, "d")));
                        break;
                    case "polyline":
                        var nums = Numbers(Attr(attrs, "points"));
                        var pl = new List<Vector2>();
                        for (int i = 0; i + 1 < nums.Count; i += 2) pl.Add(new Vector2(nums[i], nums[i + 1]));
                        result.Add(pl);
                        break;
                    case "line":
                        result.Add(new List<Vector2>
                        {
                            new(F(Attr(attrs, "x1")), F(Attr(attrs, "y1"))),
                            new(F(Attr(attrs, "x2")), F(Attr(attrs, "y2"))),
                        });
                        break;
                }
            }
            return result;
        }

        static string Attr(string attrs, string name)
        {
            var m = Regex.Match(attrs, $@"\b{name}\s*=\s*""([^""]*)""");
            return m.Success ? m.Groups[1].Value : "";
        }

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static List<float> Numbers(string s)
        {
            var o = new List<float>();
            foreach (Match m in Regex.Matches(s, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")) o.Add(F(m.Value));
            return o;
        }

        /// Lecteur de l'attribut `d` : jetons nombres et commandes, drapeaux d'arc sur un seul chiffre.
        sealed class Reader
        {
            readonly string _s;
            int _i;
            public Reader(string s) { _s = s; }

            void SkipSeparators()
            {
                while (_i < _s.Length && (char.IsWhiteSpace(_s[_i]) || _s[_i] == ',')) _i++;
            }

            public bool AtEnd { get { SkipSeparators(); return _i >= _s.Length; } }

            public bool NextIsNumber
            {
                get
                {
                    SkipSeparators();
                    if (_i >= _s.Length) return false;
                    char c = _s[_i];
                    return char.IsDigit(c) || c == '-' || c == '+' || c == '.';
                }
            }

            public char Command()
            {
                SkipSeparators();
                return _s[_i++];
            }

            public float Number()
            {
                SkipSeparators();
                int start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                bool dot = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (char.IsDigit(c)) { _i++; continue; }
                    if (c == '.' && !dot) { dot = true; _i++; continue; }
                    if ((c == 'e' || c == 'E') && _i + 1 < _s.Length)
                    {
                        _i++;
                        if (_s[_i] == '-' || _s[_i] == '+') _i++;
                        continue;
                    }
                    break;
                }
                return F(_s.Substring(start, _i - start));
            }

            public bool Flag()
            {
                SkipSeparators();
                return _s[_i++] == '1';
            }
        }

        public static List<List<Vector2>> ParsePath(string d)
        {
            var paths = new List<List<Vector2>>();
            List<Vector2> cur = null;
            var r = new Reader(d);
            Vector2 p = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
            char cmd = ' ', prev = ' ';
            while (!r.AtEnd)
            {
                if (!r.NextIsNumber) cmd = r.Command();
                else if (cmd == 'M') cmd = 'L';
                else if (cmd == 'm') cmd = 'l';
                bool rel = char.IsLower(cmd);
                char up = char.ToUpperInvariant(cmd);
                Vector2 Pt() { var x = r.Number(); var y = r.Number(); return rel ? p + new Vector2(x, y) : new Vector2(x, y); }
                switch (up)
                {
                    case 'M':
                        p = Pt(); start = p;
                        cur = new List<Vector2> { p };
                        paths.Add(cur);
                        break;
                    case 'L':
                        p = Pt(); Add(ref cur, paths, p);
                        break;
                    case 'H':
                        { var x = r.Number(); p = new Vector2(rel ? p.x + x : x, p.y); Add(ref cur, paths, p); }
                        break;
                    case 'V':
                        { var y = r.Number(); p = new Vector2(p.x, rel ? p.y + y : y); Add(ref cur, paths, p); }
                        break;
                    case 'C':
                        {
                            var c1 = Pt(); var c2 = Pt(); var e = Pt();
                            Cubic(ref cur, paths, p, c1, c2, e);
                            lastCtrl = c2; p = e;
                        }
                        break;
                    case 'S':
                        {
                            var c1 = (prev == 'C' || prev == 'S') ? 2 * p - lastCtrl : p;
                            var c2 = Pt(); var e = Pt();
                            Cubic(ref cur, paths, p, c1, c2, e);
                            lastCtrl = c2; p = e;
                        }
                        break;
                    case 'Q':
                        {
                            var c = Pt(); var e = Pt();
                            Cubic(ref cur, paths, p, p + 2f / 3f * (c - p), e + 2f / 3f * (c - e), e);
                            lastCtrl = c; p = e;
                        }
                        break;
                    case 'A':
                        {
                            float rx = r.Number(), ry = r.Number(), rot = r.Number();
                            bool large = r.Flag(), sweep = r.Flag();
                            var e = Pt();
                            Arc(ref cur, paths, p, rx, ry, rot, large, sweep, e);
                            p = e;
                        }
                        break;
                    case 'Z':
                        p = start; Add(ref cur, paths, p);
                        cur = null;
                        break;
                    default:
                        throw new FormatException($"Commande SVG non prise en charge : {cmd}");
                }
                prev = up;
            }
            return paths;
        }

        static void Add(ref List<Vector2> cur, List<List<Vector2>> paths, Vector2 p)
        {
            if (cur == null) { cur = new List<Vector2>(); paths.Add(cur); }
            if (cur.Count == 0 || (cur[^1] - p).sqrMagnitude > 1e-10f) cur.Add(p);
        }

        static void Cubic(ref List<Vector2> cur, List<List<Vector2>> paths, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (cur == null) { cur = new List<Vector2> { a }; paths.Add(cur); }
            for (int i = 1; i <= CurveSteps; i++)
            {
                float t = i / (float)CurveSteps, u = 1 - t;
                Add(ref cur, paths, u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
            }
        }

        /// Arc elliptique (paramétrage par extrémités, SVG 1.1 annexe F.6).
        static void Arc(ref List<Vector2> cur, List<List<Vector2>> paths, Vector2 p0, float rx, float ry, float rotDeg,
            bool large, bool sweep, Vector2 p1)
        {
            if (cur == null) { cur = new List<Vector2> { p0 }; paths.Add(cur); }
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            if (rx < 1e-6f || ry < 1e-6f) { Add(ref cur, paths, p1); return; }
            float phi = rotDeg * Mathf.Deg2Rad, cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            var dp = (p0 - p1) / 2f;
            var x1 = new Vector2(cos * dp.x + sin * dp.y, -sin * dp.x + cos * dp.y);
            float lambda = x1.x * x1.x / (rx * rx) + x1.y * x1.y / (ry * ry);
            if (lambda > 1) { float s = Mathf.Sqrt(lambda); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * x1.y * x1.y - ry * ry * x1.x * x1.x;
            float den = rx * rx * x1.y * x1.y + ry * ry * x1.x * x1.x;
            float coef = Mathf.Sqrt(Mathf.Max(0, num / Mathf.Max(den, 1e-12f))) * (large == sweep ? -1 : 1);
            var c1 = coef * new Vector2(rx * x1.y / ry, -ry * x1.x / rx);
            var center = new Vector2(cos * c1.x - sin * c1.y, sin * c1.x + cos * c1.y) + (p0 + p1) / 2f;
            float Angle(Vector2 u, Vector2 v) => Mathf.Atan2(u.x * v.y - u.y * v.x, Vector2.Dot(u, v));
            var u0 = new Vector2((x1.x - c1.x) / rx, (x1.y - c1.y) / ry);
            var u1 = new Vector2((-x1.x - c1.x) / rx, (-x1.y - c1.y) / ry);
            float theta = Angle(Vector2.right, u0);
            float delta = Angle(u0, u1);
            if (!sweep && delta > 0) delta -= 2 * Mathf.PI;
            else if (sweep && delta < 0) delta += 2 * Mathf.PI;
            int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 16)));
            for (int i = 1; i <= steps; i++)
            {
                float a = theta + delta * i / steps;
                float ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                Add(ref cur, paths, i == steps ? p1 : center + new Vector2(cos * ex - sin * ey, sin * ex + cos * ey));
            }
        }
    }
}
