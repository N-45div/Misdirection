using System;
using System.Collections.Generic;
using Misdirection.Core.Table;
using UnityEngine;

namespace Misdirection.Table
{
    /// <summary>
    /// Remembers the player's table between sessions with a Meta spatial anchor: whenever the table is
    /// set (palms or room scan) the anchor is saved; on the next launch it is loaded and relocalised,
    /// so the mat appears in the same place before the player does anything. A palms calibration
    /// always wins and replaces the saved anchor.
    /// </summary>
    [DefaultExecutionOrder(-16)]
    public sealed class TableAnchorStore : MonoBehaviour
    {
        public const string PrefsKey = "misdirection.table.anchor";

        [SerializeField] private TableCalibrator table;
        [SerializeField] private bool restoreOnStart = true;
        [SerializeField] private double localizeTimeoutSeconds = 5.0;

        private OVRSpatialAnchor current;
        private bool restoring;
        private bool saving;

        public string Status { get; private set; }

        public void Configure(TableCalibrator calibrator)
        {
            table = calibrator;
        }

        private void OnEnable()
        {
            if (table != null)
            {
                table.OnCalibrated += OnCalibrated;
            }
        }

        private void OnDisable()
        {
            if (table != null)
            {
                table.OnCalibrated -= OnCalibrated;
            }
        }

        private void Start()
        {
            if (restoreOnStart)
            {
                Restore();
            }
        }

        /// <summary>Forget the saved table (e.g. from a settings menu).</summary>
        public void Forget()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            DestroyCurrent(true);
            Status = "Saved table forgotten.";
        }

        private async void Restore()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            Guid uuid;
            if (string.IsNullOrEmpty(saved) || !Guid.TryParse(saved, out uuid))
            {
                Status = "No saved table yet.";
                return;
            }
            restoring = true;
            try
            {
                List<OVRSpatialAnchor.UnboundAnchor> found = new List<OVRSpatialAnchor.UnboundAnchor>();
                var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[] { uuid }, found);
                if (!result.Success || found.Count == 0)
                {
                    Status = "Saved table not found in this space.";
                    return;
                }
                OVRSpatialAnchor.UnboundAnchor unbound = found[0];
                if (!unbound.Localized && !await unbound.LocalizeAsync(localizeTimeoutSeconds))
                {
                    Status = "Couldn't relocate the saved table here.";
                    return;
                }
                Pose pose;
                if (!unbound.TryGetPose(out pose))
                {
                    Status = "Saved table has no pose yet.";
                    return;
                }
                GameObject go = new GameObject("Saved Table Anchor");
                go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                current = go.AddComponent<OVRSpatialAnchor>();
                unbound.BindTo(current);

                // The player's palms always win; otherwise the saved table beats the room scan's guess.
                if (table != null && table.Source != TableSource.Palms)
                {
                    Vector3 forward = pose.rotation * Vector3.forward;
                    table.SetFrame(new TableFrame(pose.position.ToNumerics(), forward.ToNumerics()), TableSource.SavedAnchor);
                }
                Status = "Restored your table from last time.";
                Debug.Log("[Misdirection] " + Status + " (" + uuid + ")");
            }
            catch (Exception e)
            {
                Status = "Saved table unavailable: " + e.Message;
                Debug.LogWarning("[Misdirection] " + Status);
            }
            finally
            {
                restoring = false;
                // A table set while we were restoring (and not replaced by the restore) still needs saving.
                if (current == null && table != null && table.Calibrated && table.Source != TableSource.SavedAnchor)
                {
                    Save(table.Frame);
                }
            }
        }

        private void OnCalibrated()
        {
            if (restoring || table == null || table.Source == TableSource.SavedAnchor)
            {
                return;
            }
            Save(table.Frame);
        }

        private async void Save(TableFrame frame)
        {
            if (saving)
            {
                return;
            }
            saving = true;
            try
            {
                DestroyCurrent(true);
                Vector3 origin = frame.Origin.ToUnity();
                Quaternion rotation = Quaternion.LookRotation(frame.Forward.ToUnity(), Vector3.up);
                GameObject go = new GameObject("Table Anchor");
                go.transform.SetPositionAndRotation(origin, rotation);
                OVRSpatialAnchor anchor = go.AddComponent<OVRSpatialAnchor>();
                current = anchor;
                if (!await anchor.WhenCreatedAsync())
                {
                    Status = "Couldn't create a table anchor (anchors unsupported here?).";
                    return;
                }
                var result = await anchor.SaveAnchorAsync();
                if (!result.Success)
                {
                    Status = "Couldn't save the table anchor (" + result.Status + ").";
                    return;
                }
                PlayerPrefs.SetString(PrefsKey, anchor.Uuid.ToString());
                PlayerPrefs.Save();
                Status = "Table saved for next time.";
                Debug.Log("[Misdirection] " + Status + " (" + anchor.Uuid + ")");
            }
            catch (Exception e)
            {
                Status = "Table not saved: " + e.Message;
                Debug.LogWarning("[Misdirection] " + Status);
            }
            finally
            {
                saving = false;
            }
        }

        private async void DestroyCurrent(bool erase)
        {
            OVRSpatialAnchor old = current;
            current = null;
            if (old == null)
            {
                return;
            }
            if (erase && old.Created)
            {
                try
                {
                    await old.EraseAnchorAsync();
                }
                catch (Exception)
                {
                    // Erasing is best-effort; the PlayerPrefs key is what we restore from.
                }
            }
            if (old != null)
            {
                Destroy(old.gameObject);
            }
        }
    }
}
