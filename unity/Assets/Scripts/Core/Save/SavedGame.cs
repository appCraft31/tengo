#nullable enable
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Partie en cours sauvegardée (GameState.swift). Format JSON identique à
    /// celui de `JSONEncoder` sur iOS : `{"cells":[[{"value","row","col",
    /// "isAnchored","isFrozen","bloom"} | null]], "score", "blocked":[[bool]]}`.
    /// Décodage tolérant : `isAnchored`, `isFrozen`, `bloom` et `blocked` sont
    /// absents des sauvegardes antérieures aux twists.
    public sealed class SavedGame
    {
        public SavedGame(GridModel grid, int score)
        {
            Grid = grid;
            Score = score;
        }

        public GridModel Grid { get; }
        public int Score { get; }

        public string ToJson()
        {
            var cells = new JArray(Grid.Cells.Select(row => new JArray(row.Select(b => b == null
                ? (JToken)JValue.CreateNull()
                : new JObject
                {
                    ["value"] = b.Value,
                    ["row"] = b.Row,
                    ["col"] = b.Col,
                    ["isAnchored"] = b.IsAnchored,
                    ["isFrozen"] = b.IsFrozen,
                    ["bloom"] = b.Bloom,
                }))));
            var blocked = new JArray(Grid.Blocked.Select(row => new JArray(row.Select(v => (JToken)v))));
            var root = new JObject { ["cells"] = cells, ["score"] = Score, ["blocked"] = blocked };
            return root.ToString(Formatting.None);
        }

        /// `null` si le JSON est illisible ou de mauvaise forme.
        public static SavedGame? FromJson(string json)
        {
            try
            {
                JObject root = JObject.Parse(json);
                if (!(root["cells"] is JArray rows) || rows.Count != GameRules.Rows) return null;
                var cells = new BubbleModel?[GameRules.Rows][];
                for (int r = 0; r < GameRules.Rows; r++)
                {
                    if (!(rows[r] is JArray row) || row.Count != GameRules.Cols) return null;
                    cells[r] = new BubbleModel?[GameRules.Cols];
                    for (int c = 0; c < GameRules.Cols; c++)
                    {
                        if (!(row[c] is JObject o)) continue;
                        cells[r][c] = new BubbleModel(
                            (int)o["value"]!,
                            (int)o["row"]!,
                            (int)o["col"]!,
                            (bool?)o["isAnchored"] ?? false,
                            (bool?)o["isFrozen"] ?? false,
                            (int?)o["bloom"] ?? 0);
                    }
                }
                bool[][]? blocked = null;
                if (root["blocked"] is JArray mask && mask.Count == GameRules.Rows)
                {
                    blocked = new bool[GameRules.Rows][];
                    for (int r = 0; r < GameRules.Rows; r++)
                    {
                        blocked[r] = new bool[GameRules.Cols];
                        if (!(mask[r] is JArray row)) continue;
                        for (int c = 0; c < GameRules.Cols && c < row.Count; c++) blocked[r][c] = (bool)row[c];
                    }
                }
                return new SavedGame(GridModel.FromSnapshot(cells, blocked), (int?)root["score"] ?? 0);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (System.FormatException)
            {
                return null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }
}
