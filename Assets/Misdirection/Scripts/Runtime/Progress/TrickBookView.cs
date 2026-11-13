using System;
using System.Collections.Generic;
using Misdirection.Core.Progress;
using TMPro;
using UnityEngine;

namespace Misdirection.Progress
{
    /// <summary>
    /// The trick book that stands on the table beside the mat at the bow (or the walk-off): each trick
    /// with its 0-3 star coins and what the next star takes, today's challenge (OK / not yet), the
    /// streak and best tips, and what tomorrow brings. A small card that always faces the player.
    /// Built from primitives with three shared materials (page, gold, grey): one material per colour,
    /// none per renderer. Plain class owned by the ProgressDirector, which calls <see cref="Update"/>.
    /// </summary>
    public sealed class TrickBookView : IDisposable
    {
        private const float Width = 0.31f;
        private const float Height = 0.25f;
        private const float RowTop = 0.070f;
        private const float RowStep = 0.052f;
        private const float CoinDiameter = 0.036f;
        private static readonly float[] CoinX = { 0.035f, 0.080f, 0.125f };

        private readonly GameObject root;
        private readonly TextMeshPro title;
        private readonly TextMeshPro footer;
        private readonly List<TextMeshPro> names = new List<TextMeshPro>();
        private readonly List<TextMeshPro> hints = new List<TextMeshPro>();
        private readonly List<Renderer[]> coins = new List<Renderer[]>();
        private readonly List<bool[]> justEarned = new List<bool[]>();
        private Material page;
        private Material gold;
        private Material grey;

        public TrickBookView(Transform parent)
        {
            root = new GameObject("Trick Book");
            root.transform.SetParent(parent, false);

            // The page: a thin dark card just behind the text.
            GameObject card = Primitive(PrimitiveType.Cube, "Page", new Vector3(0f, 0f, 0.004f), new Vector3(Width, Height, 0.004f));
            Renderer cardRenderer = card.GetComponent<Renderer>();
            page = Tinted(cardRenderer, new Color(0.09f, 0.07f, 0.13f));
            gold = Tinted(cardRenderer, new Color(1f, 0.8f, 0.2f));
            grey = Tinted(cardRenderer, new Color(0.35f, 0.35f, 0.38f));
            cardRenderer.sharedMaterial = page;

            title = Label("Title", new Vector2(0f, 0.105f), new Vector2(Width - 0.02f, 0.03f), 0.2f, TextAlignmentOptions.Center);
            title.text = "<b>Trick Book</b>";
            for (int trick = 0; trick < TrickBook.TrickCount; trick++)
            {
                float y = RowTop - trick * RowStep;
                TextMeshPro name = Label("Trick " + (trick + 1), new Vector2(-0.07f, y), new Vector2(0.15f, 0.03f), 0.26f, TextAlignmentOptions.Left);
                name.text = TrickBook.NameOf(trick);
                names.Add(name);
                hints.Add(Label("Trick " + (trick + 1) + " Next Star", new Vector2(0f, y - 0.023f), new Vector2(Width - 0.02f, 0.017f), 0.13f,
                                TextAlignmentOptions.Left));
                Renderer[] row = new Renderer[TrickBook.MaxStars];
                for (int star = 0; star < row.Length; star++)
                {
                    // A coin on edge, its face to the player: the same star coins as the show's rating. It stands
                    // clear of the page so a turning coin never cuts into it.
                    GameObject coin = Primitive(PrimitiveType.Cylinder, "Trick " + (trick + 1) + " Star " + (star + 1),
                                                new Vector3(CoinX[star], y, -0.022f), new Vector3(CoinDiameter, 0.002f, CoinDiameter));
                    coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    row[star] = coin.GetComponent<Renderer>();
                    row[star].sharedMaterial = grey;
                }
                coins.Add(row);
                justEarned.Add(new bool[row.Length]);
            }
            footer = Label("Today", new Vector2(0f, -0.066f), new Vector2(Width - 0.02f, 0.09f), 0.17f, TextAlignmentOptions.Center);
            root.SetActive(false);
        }

        public bool Visible
        {
            get { return root != null && root.activeSelf; }
        }

        /// <summary>Fills the book in after a show and stands it up.</summary>
        /// <param name="regularAlreadyComes">The player is already a returning player (so tomorrow is "a new crowd", not "the Regular comes").</param>
        public void Show(ProgressData data, DailyChallenge challenge, ShowOutcome outcome, bool regularAlreadyComes)
        {
            if (root == null || data == null || outcome == null)
            {
                return;
            }
            for (int trick = 0; trick < coins.Count; trick++)
            {
                TrickMastery mastery = trick < data.Tricks.Length ? data.Tricks[trick] : new TrickMastery();
                int stars = mastery.Stars;
                int gained = trick < outcome.StarsGained.Length ? outcome.StarsGained[trick] : 0;
                for (int star = 0; star < coins[trick].Length; star++)
                {
                    coins[trick][star].sharedMaterial = star < stars ? gold : grey;
                    // The stars this show earned are the last ones lit: they turn, the older ones rest.
                    justEarned[trick][star] = star < stars && star >= stars - gained;
                    coins[trick][star].transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                hints[trick].text = "<color=#B8B4C8>" + ProgressText.NextStarLine(mastery) + "</color>";
            }

            string verdict = outcome.ChallengePassedToday
                ? "<color=#7CFC00><b>" + ProgressText.ChallengeVerdict(true) + "</b></color>"
                : "<color=#FFB74D><b>" + ProgressText.ChallengeVerdict(false) + "</b></color>";
            string best = ProgressText.BestLine(data.BestTips, outcome.NewBestTips);
            footer.text = verdict + "\n" +
                          "<size=80%>" + (challenge != null ? challenge.Text : "") + "</size>\n" +
                          ProgressText.StreakLine(outcome.Streak) + "  -  " + best + "\n" +
                          "<size=80%><color=#B8B4C8>" + ProgressText.TomorrowLine(regularAlreadyComes) + "</color></size>";
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        /// <summary>Keeps the book at <paramref name="position"/>, facing <paramref name="viewer"/>, and turns the newly earned coins.</summary>
        public void Update(double now, Vector3 position, Vector3 viewer)
        {
            if (!Visible)
            {
                return;
            }
            root.transform.position = position;
            // Tilted back to face the eyes, like a card propped on the table.
            WorldText.Face(root.transform, viewer, false);
            for (int trick = 0; trick < coins.Count; trick++)
            {
                for (int star = 0; star < coins[trick].Length; star++)
                {
                    if (justEarned[trick][star])
                    {
                        coins[trick][star].transform.localRotation =
                            Quaternion.Euler(0f, (float)(now * 120.0) + star * 40f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                    }
                }
            }
        }

        public void Dispose()
        {
            DestroyNow(page);
            DestroyNow(gold);
            DestroyNow(grey);
            page = null;
            gold = null;
            grey = null;
        }

        private TextMeshPro Label(string name, Vector2 at, Vector2 size, float maxFontSize, TextAlignmentOptions alignment)
        {
            TextMeshPro text = WorldText.Create(root.transform, name, size, maxFontSize);
            text.transform.localPosition = new Vector3(at.x, at.y, 0f);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private GameObject Primitive(PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                UnityEngine.Object.Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>One shared material per colour, copied from the primitives' default material (so it suits the render pipeline).</summary>
        private static Material Tinted(Renderer template, Color color)
        {
            Material m = new Material(template.sharedMaterial);
            m.name = "Trick Book " + ColorUtility.ToHtmlStringRGB(color);
            m.color = color;
            return m;
        }

        private static void DestroyNow(Material m)
        {
            if (m != null)
            {
                UnityEngine.Object.Destroy(m);
            }
        }
    }
}
