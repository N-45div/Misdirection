using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using Misdirection.Core.Crowd;
using Misdirection.Core.Table;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace Misdirection.Table
{
    /// <summary>
    /// Reads the Quest's room scan through MR Utility Kit: picks the table the player is sitting at
    /// and hands it to the <see cref="TableCalibrator"/>, and turns walls and furniture into a floor
    /// plan so the crowd stands on open floor. Without a scan (or MRUK) the palms gesture still works.
    /// Never overrides a table the player set with their palms or one restored from last session.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class RoomTableFinder : MonoBehaviour
    {
        [SerializeField] private TableCalibrator table;
        [SerializeField] private LookSource lookSource;
        [SerializeField] private TableSearchSettings search = new TableSearchSettings();
        [Tooltip("Room-scan furniture nobody can stand on. Walls always count.")]
        [SerializeField] private MRUKAnchor.SceneLabels furnitureLabels =
            MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.STORAGE |
            MRUKAnchor.SceneLabels.BED | MRUKAnchor.SceneLabels.SCREEN | MRUKAnchor.SceneLabels.PLANT |
            MRUKAnchor.SceneLabels.LAMP | MRUKAnchor.SceneLabels.OTHER;
        [Tooltip("Seconds to wait for the scan before telling the player to use their palms.")]
        [SerializeField] private float scanTimeoutSeconds = 6f;
        [Tooltip("The scan can land before the head is tracked; keep re-checking the tables this long (seconds).")]
        [SerializeField] private float retrySeconds = 15f;

        private readonly List<Segment2> walls = new List<Segment2>();
        private readonly List<Footprint> furniture = new List<Footprint>();
        private readonly List<TableCandidate> candidates = new List<TableCandidate>();
        private readonly List<MRUKAnchor> candidateAnchors = new List<MRUKAnchor>();
        private MRUKRoom room;
        private MRUKAnchor chosenTable;
        private float startTime;
        private float retryUntil;
        private float nextRetry;

        public bool RoomLoaded { get; private set; }
        public bool HasFloor { get; private set; }
        public float FloorY { get; private set; }
        public bool FoundTable { get { return chosenTable != null; } }
        public IList<Segment2> Walls { get { return walls; } }
        public IList<Footprint> Furniture { get { return furniture; } }

        /// <summary>One line for HUDs and logs.</summary>
        public string Status { get; private set; }

        /// <summary>Fires after the room scan is read (walls and furniture are ready).</summary>
        public event Action OnRoomLoaded;

        public void Configure(TableCalibrator calibrator, LookSource look)
        {
            table = calibrator;
            lookSource = look;
        }

        private void Start()
        {
            startTime = Time.time;
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
            if (MRUK.Instance == null)
            {
                Status = "No room scanner in this scene: rest your palms on the table.";
                return;
            }
            Status = "Reading your room scan…";
            MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
        }

        private void Update()
        {
            if (!RoomLoaded && MRUK.Instance != null && Time.time - startTime > scanTimeoutSeconds && Status == "Reading your room scan…")
            {
                Status = "No room scan yet: rest your palms on the table.";
            }
            // The scan often arrives before head tracking settles; re-check with the current head for a while.
            if (RoomLoaded && chosenTable == null && candidates.Count > 0 && Time.time < retryUntil && Time.time >= nextRetry
                && (table == null || !table.Calibrated))
            {
                nextRetry = Time.time + 1f;
                ChooseTable(false);
            }
        }

        private void OnSceneLoaded()
        {
            room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            if (room == null)
            {
                Status = "No room scan. Set up your space in Quest settings, or rest your palms on the table.";
                return;
            }

            candidates.Clear();
            candidateAnchors.Clear();
            foreach (MRUKAnchor anchor in room.Anchors)
            {
                TableCandidate candidate;
                if (anchor != null && anchor.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE) && TryTop(anchor, out candidate))
                {
                    candidates.Add(candidate);
                    candidateAnchors.Add(anchor);
                }
            }
            HasFloor = room.FloorAnchor != null;
            FloorY = HasFloor ? room.FloorAnchor.transform.position.y : 0f;
            RoomLoaded = true;
            retryUntil = Time.time + retrySeconds;
            nextRetry = Time.time + 1f;
            ChooseTable(true);
            if (OnRoomLoaded != null)
            {
                OnRoomLoaded();
            }
        }

        /// <summary>Picks the table for the current head position; rebuilds the floor plan around it.</summary>
        private void ChooseTable(bool firstTry)
        {
            Vector3 eyes = Eyes();
            List<TableRejection> reasons = new List<TableRejection>();
            int best = TableDetection.Choose(candidates, eyes.ToNumerics(), search, reasons);
            chosenTable = best >= 0 ? candidateAnchors[best] : null;
            BuildFloorPlan(room);

            if (chosenTable != null)
            {
                TableFrame frame = TableDetection.FrameFor(candidates[best], eyes.ToNumerics(), search.MatHalfWidth);
                if (table != null && (!table.Calibrated || table.Source == TableSource.Room))
                {
                    table.SetFrame(frame, TableSource.Room);
                }
                Status = "Found your table in the room scan.";
            }
            else
            {
                Status = candidates.Count == 0
                    ? "The room scan has no table: rest your palms on the table."
                    : "The room scan's tables are " + Describe(reasons) + ": rest your palms on the table.";
            }
            if (firstTry || chosenTable != null)
            {
                Debug.Log("[Misdirection] Room scan: " + candidates.Count + " table(s), " + walls.Count + " wall(s), " +
                          furniture.Count + " furniture footprint(s). " + Status + "\n" + Diagnose(eyes, reasons));
            }
        }

        /// <summary>Eye and floor heights and why each table was or wasn't picked (for logs and the smoke test).</summary>
        public string Diagnose(Vector3 eyes, List<TableRejection> reasons)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("eyes at y=").Append(eyes.y.ToString("F2"));
            if (HasFloor)
            {
                sb.Append(" (").Append((eyes.y - FloorY).ToString("F2")).Append(" m above the floor)");
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                TableCandidate t = candidates[i];
                sb.Append("; ").Append(t.Id).Append(": top y=").Append(t.TopY.ToString("F2"))
                  .Append(", ").Append((eyes.y - t.TopY).ToString("F2")).Append(" m below the eyes, edge ")
                  .Append(TableDetection.EdgeDistance(t, eyes.ToNumerics()).ToString("F2")).Append(" m away")
                  .Append(i < reasons.Count ? " -> " + reasons[i] : "");
            }
            return sb.ToString();
        }

        /// <summary>The table top as a rectangle: from the anchor's plane, else the top face of its volume.</summary>
        private static bool TryTop(MRUKAnchor anchor, out TableCandidate candidate)
        {
            candidate = new TableCandidate();
            Transform t = anchor.transform;
            Vector3 c0, c1, c2, c3;
            if (anchor.PlaneRect.HasValue)
            {
                Rect r = anchor.PlaneRect.Value;
                c0 = t.TransformPoint(new Vector3(r.xMin, r.yMin, 0f));
                c1 = t.TransformPoint(new Vector3(r.xMax, r.yMin, 0f));
                c2 = t.TransformPoint(new Vector3(r.xMax, r.yMax, 0f));
                c3 = t.TransformPoint(new Vector3(r.xMin, r.yMax, 0f));
            }
            else if (anchor.VolumeBounds.HasValue)
            {
                // MRUK volumes point their local +Z up; pick whichever z face is higher in the world.
                Bounds b = anchor.VolumeBounds.Value;
                float zTop = t.TransformPoint(new Vector3(b.center.x, b.center.y, b.max.z)).y >=
                             t.TransformPoint(new Vector3(b.center.x, b.center.y, b.min.z)).y ? b.max.z : b.min.z;
                c0 = t.TransformPoint(new Vector3(b.min.x, b.min.y, zTop));
                c1 = t.TransformPoint(new Vector3(b.max.x, b.min.y, zTop));
                c2 = t.TransformPoint(new Vector3(b.max.x, b.max.y, zTop));
                c3 = t.TransformPoint(new Vector3(b.min.x, b.max.y, zTop));
            }
            else
            {
                return false;
            }
            float top = Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y));
            candidate = TableCandidate.FromCorners(c0.ToNumerics(), c1.ToNumerics(), c2.ToNumerics(), c3.ToNumerics(), top, anchor.name);
            return true;
        }

        private void BuildFloorPlan(MRUKRoom scannedRoom)
        {
            walls.Clear();
            furniture.Clear();
            if (scannedRoom == null)
            {
                return;
            }
            foreach (MRUKAnchor wall in scannedRoom.WallAnchors)
            {
                if (wall == null || !wall.PlaneRect.HasValue)
                {
                    continue;
                }
                // Wall planes run along local X (width) and up local Y (height).
                Rect r = wall.PlaneRect.Value;
                Vector3 a = wall.transform.TransformPoint(new Vector3(r.xMin, 0f, 0f));
                Vector3 b = wall.transform.TransformPoint(new Vector3(r.xMax, 0f, 0f));
                walls.Add(new Segment2(new NVector2(a.x, a.z), new NVector2(b.x, b.z)));
            }
            foreach (MRUKAnchor anchor in scannedRoom.Anchors)
            {
                if (anchor == null || anchor == chosenTable || !anchor.HasAnyLabel(furnitureLabels))
                {
                    continue;
                }
                TableCandidate top;
                if (TryTop(anchor, out top))
                {
                    furniture.Add(new Footprint(Flat(top.C0), Flat(top.C1), Flat(top.C2), Flat(top.C3)));
                }
            }
        }

        private Vector3 Eyes()
        {
            if (lookSource != null && lookSource.Head != null)
            {
                return lookSource.Head.position;
            }
            Camera cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.up * 1.2f;
        }

        private static NVector2 Flat(System.Numerics.Vector3 v)
        {
            return new NVector2(v.X, v.Z);
        }

        private static string Describe(List<TableRejection> reasons)
        {
            List<string> words = new List<string>();
            foreach (TableRejection r in reasons)
            {
                string w = r == TableRejection.TooFar ? "too far away" : r == TableRejection.TooSmall ? "too small" : "the wrong height";
                if (!words.Contains(w))
                {
                    words.Add(w);
                }
            }
            return string.Join(" or ", words.ToArray());
        }
    }
}
