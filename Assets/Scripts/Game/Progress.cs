using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>Best stars per level, saved on the device (PlayerPrefs; in the browser that is local storage).</summary>
    public static class Progress
    {
        private static string Key(int level) => "match3.stars." + level;

        public static int Stars(int level) => PlayerPrefs.GetInt(Key(level), 0);

        public static int TotalStars
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Levels.All.Count; i++) total += Stars(i);
                return total;
            }
        }

        public static void Record(int level, int stars)
        {
            if (stars <= Stars(level)) return;
            PlayerPrefs.SetInt(Key(level), stars);
            PlayerPrefs.Save();
        }
    }
}
