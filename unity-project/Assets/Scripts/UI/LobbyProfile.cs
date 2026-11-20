using System.Globalization;
using UnityEngine;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Persisted operator identity & wallet shown on the Home Screen.
    /// PlayerPrefs-backed so a reinstall-free session keeps its balance; the
    /// defaults match the Season 05 mock (45,820 coins / 1,240 gems).
    /// </summary>
    public static class LobbyProfile
    {
        public const string DefaultCallSign = "Commander_K";
        public const int DefaultCoins = 45820;
        public const int DefaultGems = 1240;

        private const string CoinsKey = "bh.lobby.coins";
        private const string GemsKey = "bh.lobby.gems";
        private const string CallSignKey = "bh.lobby.callsign";

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, DefaultCoins);
        public static int Gems => PlayerPrefs.GetInt(GemsKey, DefaultGems);

        public static string CallSign
        {
            get
            {
                var value = PlayerPrefs.GetString(CallSignKey, DefaultCallSign);
                return string.IsNullOrEmpty(value) ? DefaultCallSign : value;
            }
        }

        public static string FormatCoins => Coins.ToString("N0", CultureInfo.InvariantCulture);
        public static string FormatGems => Gems.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Rewards entry point for future milestones (missions, events).</summary>
        public static void GrantCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            PlayerPrefs.SetInt(CoinsKey, Coins + amount);
            PlayerPrefs.Save();
        }
    }
}
