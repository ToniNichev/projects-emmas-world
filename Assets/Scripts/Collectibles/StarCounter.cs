using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sandbox.Audio;
using Sandbox.Player;

namespace Sandbox.Collectibles
{
    // Tracks which stars this player has found and shows "Stars: 3 / 12".
    // Progress is saved in the browser (PlayerPrefs), so found stars stay
    // found between visits. Stars are per player, not shared in multiplayer.
    public class StarCounter : MonoBehaviour
    {
        private const string PrefsKey = "emmasworld.stars";

        [SerializeField] private Text label;

        private readonly HashSet<string> collected = new HashSet<string>();
        private StarCollectible[] stars;
        private SoundEffects soundEffects;

        public static StarCounter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            stars = UnityEngine.Object.FindObjectsByType<StarCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
            if (player != null)
                soundEffects = player.GetComponent<SoundEffects>();

            Load();
            foreach (StarCollectible star in stars)
            {
                if (collected.Contains(star.StarId))
                    star.gameObject.SetActive(false);
            }
            Refresh();
        }

        public void Collect(StarCollectible star)
        {
            star.gameObject.SetActive(false);
            if (!collected.Add(star.StarId))
                return;

            soundEffects?.PlayStar();
            Save();
            Refresh();
        }

        private void Refresh()
        {
            if (label == null || stars == null)
                return;

            int found = 0;
            foreach (StarCollectible star in stars)
            {
                if (collected.Contains(star.StarId))
                    found++;
            }

            label.text = found >= stars.Length
                ? $"All {stars.Length} stars found!"
                : $"Stars: {found} / {stars.Length}";
        }

        // Browser storage can be unavailable (private windows, blocked site
        // data); the game should still work, just without remembering.
        private void Load()
        {
            try
            {
                string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
                foreach (string id in saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    collected.Add(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"StarCounter: couldn't load progress ({e.Message})");
            }
        }

        private void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, string.Join(",", collected));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"StarCounter: couldn't save progress ({e.Message})");
            }
        }
    }
}
