using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Services
{
    /// Analytics simulées : journal `[Analytics] nom k=v…` et mémoire des
    /// événements (tests, pilote automatique). Remplacé par Firebase à l'étape 7.
    public sealed class LogAnalytics : IAnalytics
    {
        public readonly List<string> Events = new();
        public bool Echo = true;

        void Log(string name, string parameters = "")
        {
            string line = parameters.Length == 0 ? name : name + " " + parameters;
            Events.Add(line);
            if (Echo) Debug.Log("[Analytics] " + line);
        }

        public int Count(string name) => Events.FindAll(e => e == name || e.StartsWith(name + " ")).Count;

        public void LevelEnd(string mode, int score, bool won) => Log("level_end", $"mode={mode} score={score} success={(won ? 1 : 0)}");
        public void DailyChallengePlayed(int score) => Log("daily_challenge_played", $"score={score}");
        public void MirrorInsightShown(string kind) => Log("mirror_insight_shown", $"kind={kind}");
        public void SeedPlanted(int power, bool strengthened) => Log("seed_planted", $"power={power} strengthened={(strengthened ? 1 : 0)}");
        public void FirstGameStart(string mode, long seconds) => Log("first_game_start", $"mode={mode} seconds_since_first_open={seconds}");
        public void FirstGameComplete(string mode, int score, bool won, long seconds) =>
            Log("first_game_complete", $"mode={mode} score={score} success={(won ? 1 : 0)} seconds_since_first_open={seconds}");
        public void FlowerUsed(int power, int chainLength) => Log("flower_used", $"power={power} length={chainLength}");
        public void FlowerPlaced(int power) => Log("flower_placed", $"power={power}");
        public void BoosterUsed(string id) => Log("booster_used", $"item_name={id}");
        public void BoosterCoachShown(string id) => Log("booster_coach_shown", $"item_name={id}");
        public void RewardedOfferView(string placement) => Log("rewarded_offer_view", $"placement={placement}");
        public void RewardedStarted(string placement) => Log("rewarded_started", $"placement={placement}");
        public void RewardedCompleted(string placement) => Log("rewarded_completed", $"placement={placement}");
        public void NoAdsOfferView(string source, int adsSeen, int adsToday) => Log("noads_offer_view", $"source={source} ads_seen={adsSeen} ads_today={adsToday}");
        public void NoAdsOfferDeclined(string source) => Log("noads_offer_decline", $"source={source}");
        public void ShopOpenedFromGame(string reason) => Log("shop_from_game", $"reason={reason}");
        public void TutorialComplete() => Log("tutorial_complete", "");
        public void SoundToggled(bool enabled) => Log("sound_toggled", $"enabled={(enabled ? 1 : 0)}");
        public void BoosterPurchased(string id, int quantity, int cost, string source) =>
            Log("spend_virtual_currency", $"item_name={id} virtual_currency_name=coins value={cost} quantity={quantity} source={source}");
        public void BoosterRefunded(string id, int amount) =>
            Log("earn_virtual_currency", $"virtual_currency_name=coins value={amount} item_name={id} reason=tutorial_refund");
        public void ShopGuide(string step) => Log("shop_guide", $"step={step}");
        public void ShopOpened(string source) => Log("shop_opened", $"source={source}");
        public void NoAdsOfferView(string source) => Log("noads_offer_view", $"source={source}");
        public void MirrorOpened(int gamesMeasured, int repertoire) => Log("mirror_opened", $"games={gamesMeasured} repertoire={repertoire}");
    }
}
