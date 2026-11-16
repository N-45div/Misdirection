#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Misdirection.Access;
using Misdirection.Audience;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Show;
using Misdirection.Table;
using Misdirection.Tricks;
using UnityEngine;

namespace Misdirection.Trailer
{
    /// <summary>
    /// Editor-only trailer capture: plays the real game with scripted head and hand input and writes
    /// every frame (fixed 30 fps) to disk, with markers for each shot. Nothing about the game is faked:
    /// the input stands in for a player, and the tricks, crowd, tutorial and pause run their real code.
    /// Started by Misdirection.EditorTools.TrailerCapture via PlayerPrefs; never present in a build.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TrailerAutopilot : MonoBehaviour
    {
        public const string SessionKey = "misdirection.trailer.session";
        public const string OutKey = "misdirection.trailer.out";
        public const string DoneKey = "misdirection.trailer.done";
        private const int Fps = 30;
        private static readonly Vector3 Eyes = new Vector3(0f, 1.15f, 0f);
        private static readonly Vector3 PinchLocal = new Vector3(0f, -0.025f, 0.12f);

        private OVRCameraRig rig;
        private Camera cam;
        private RenderTexture rt;
        private Texture2D readback;
        private string outDir;
        private int frame;
        private bool recording;
        private readonly List<string> marks = new List<string>();
        private readonly Dictionary<string, int> open = new Dictionary<string, int>();

        private ShowDirector show;
        private TableCalibrator table;
        private Crowd crowd;
        private TrickDirector vanish;
        private CupTrickDirector cupTrick;
        private HandRig right;
        private HandRig left;

        private Quaternion headRot = Quaternion.identity;
        private Quaternion headFrom, headTo;
        private float headStart, headDur;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(SessionKey, "")))
            {
                return;
            }
            new GameObject("Trailer Autopilot").AddComponent<TrailerAutopilot>();
        }

        private float T { get { return frame / (float)Fps; } }

        private void Awake()
        {
            Time.captureFramerate = Fps;
            // Keep playing when the Editor window isn't focused (the person may be using the PC).
            Application.runInBackground = true;
            outDir = PlayerPrefs.GetString(OutKey, "D:/video/capture/session");
            Directory.CreateDirectory(outDir);
            show = FindFirstObjectByType<ShowDirector>();
            table = FindFirstObjectByType<TableCalibrator>();
            crowd = FindFirstObjectByType<Crowd>();
            vanish = FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include);
            cupTrick = FindFirstObjectByType<CupTrickDirector>(FindObjectsInactive.Include);
            rig = FindFirstObjectByType<OVRCameraRig>();
            if (show != null)
            {
                show.IgnoreInputFocus = true;
            }
            // No room scan in the Editor: MRUK would load one of its sample rooms (furniture where our
            // stand-in table is) and re-place the crowd mid-show. The stand-in room below replaces it.
            foreach (RoomTableFinder finder in FindObjectsByType<RoomTableFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                finder.enabled = false;
            }
            Meta.XR.MRUtilityKit.MRUK mruk = FindFirstObjectByType<Meta.XR.MRUtilityKit.MRUK>();
            if (mruk != null)
            {
                mruk.gameObject.SetActive(false);
            }
            if (crowd != null)
            {
                crowd.SetRoom(null);
            }
            BuildRoom();
            Material skin = SharedMaterials.Lit(new Color(0.86f, 0.68f, 0.56f));
            foreach (TrackedHand h in FindObjectsByType<TrackedHand>(FindObjectsSortMode.None))
            {
                if (h.Side == HandId.Right) right = new HandRig(h, false, transform, skin);
                else if (h.Side == HandId.Left) left = new HandRig(h, true, transform, skin);
            }
            headRot = headFrom = headTo = Quaternion.LookRotation(new Vector3(0f, -0.55f, 1f), Vector3.up);
        }

        private void Start()
        {
            cam = rig != null && rig.centerEyeAnchor != null ? rig.centerEyeAnchor.GetComponent<Camera>() : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Trailer] no camera");
                return;
            }
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.045f, 0.06f, 1f);
            rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            readback = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            StartCoroutine(Capture());
            StartCoroutine(Run());
        }

        private void Update()
        {
            float k = headDur <= 0f ? 1f : Mathf.Clamp01((T - headStart) / headDur);
            k = k * k * (3f - 2f * k);
            headRot = Quaternion.Slerp(headFrom, headTo, k);
            // A little breathing so the view never looks locked off.
            Quaternion sway = Quaternion.Euler(Mathf.Sin(T * 0.7f) * 0.35f, Mathf.Sin(T * 0.45f) * 0.5f, 0f);
            if (rig != null)
            {
                rig.transform.SetPositionAndRotation(Eyes, headRot * sway);
            }
            if (right != null) right.Apply();
            if (left != null) left.Apply();
        }

        private IEnumerator Capture()
        {
            WaitForEndOfFrame end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (recording && rt != null)
                {
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(outDir, "f_" + frame.ToString("D6") + ".jpg"), readback.EncodeToJPG(93));
                }
                frame++;
            }
        }

        // ------------------------------------------------------------------ the performance

        private IEnumerator Run()
        {
            yield return Wait(0.5f);
            recording = true;
            Mark("03_tutorial_game");
            Log("setup: ghost hands demonstrate, then palms on the table");
            right.Hide(); left.Hide();
            yield return Wait(3.0f);

            // Bring both hands in and rest them flat (the real calibration gesture).
            Vector3 lp = new Vector3(-0.12f, 0.752f, 0.36f), rp = new Vector3(0.12f, 0.752f, 0.36f);
            Quaternion flat = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            left.Show(lp + new Vector3(-0.05f, 0.10f, -0.20f), flat, HandPose.Flat, 0f, 1f);
            right.Show(rp + new Vector3(0.05f, 0.10f, -0.20f), flat, HandPose.Flat, 0f, 1f);
            StartCoroutine(left.MoveWrist(lp, flat, 1.2f, this));
            yield return right.MoveWrist(rp, flat, 1.2f, this);
            yield return Until(() => table != null && table.Calibrated, 8f, "table calibrated");
            Log("table from palms");
            StartCoroutine(left.MoveWrist(lp + new Vector3(-0.08f, 0.06f, -0.12f), flat, 0.8f, this));
            yield return right.MoveWrist(rp + new Vector3(0.10f, 0.06f, -0.10f), flat, 0.8f, this);
            left.Hide();
            yield return Until(() => show.Flow.Stage == ShowStage.Tutorial && Kid() != null, 6f, "tutorial with the Kid");
            LookAt(MatWorld(0f, 0f, 0.24f), 0.5f);
            yield return Wait(2.2f);

            yield return VanishSequence(Kid(), true);
            Log("tutorial vanish revealed");
            LookAt(Kid().EyesPosition, 0.4f);
            yield return Wait(2.5f);
            End("03_tutorial_game");
            yield return Until(() => show.Flow.Stage == ShowStage.Performing && crowd.Spectators.Count >= 3, 8f, "act 1");
            yield return right.MoveWrist(MatWorld(0.16f, 0.10f, 0.05f), flat, 0.6f, this);
            LookAt(CrowdCentre(), 0.6f);
            yield return Wait(2.5f);

            // Access Tray toggled by looking (only while the coin rests).
            Mark("09_access_game");
            AccessTray tray = FindFirstObjectByType<AccessTray>();
            if (tray != null)
            {
                LookAt(tray.TokenPosition(AccessToggle.HighContrast), 0.5f);
                yield return Wait(2.6f);
                LookAt(CrowdCentre(), 0.6f);
                yield return Wait(3.0f);
                LookAt(tray.TokenPosition(AccessToggle.HighContrast), 0.5f);
                yield return Wait(2.6f);
                LookAt(CrowdCentre(), 0.6f);
                yield return Wait(1.0f);
            }
            End("09_access_game");

            // Act 1: the rule, the hook, the production.
            Mark("02_rule_game");
            Spectator tourist = Find(Archetype.Tourist), kid = Kid(), skeptic = Find(Archetype.Skeptic);
            yield return PickUpAndShow();
            LookAt(tourist.EyesPosition, 0.45f); yield return Wait(1.3f);
            LookAt(kid.EyesPosition, 0.45f); yield return Wait(1.3f);
            if (skeptic != null) { LookAt(skeptic.EyesPosition, 0.45f); yield return Wait(1.1f); }
            Mark("01_hook_game");
            LookAt(tourist.EyesPosition, 0.45f);
            yield return Until(() => tourist.Mind.EyeContact, 3f, "eye contact");
            yield return Wait(0.4f);
            yield return Ditch();
            yield return ShowEmpty();
            yield return Wait(2.6f);
            End("01_hook_game");
            End("02_rule_game");

            Mark("04_produce_game");
            LookAt(kid.EyesPosition, 0.4f);
            yield return Until(() => kid.Mind.EyeContact, 3f, "eye contact for the steal");
            yield return Steal();
            Vector3 ear = kid.EarPosition;
            Vector3 toward = Vector3.Lerp(MatWorld(0.05f, 0.18f, 0.20f), ear, 0.18f);
            yield return right.MovePinch(toward, Reach(25f), HandPose.Pinch, 1f, 0.2f, 0.9f, this);
            yield return Until(() => vanish.Trick.Phase == TrickPhase.Finale, 4f, "production");
            Log("produced from the Kid's ear");
            yield return Wait(3.6f);
            End("04_produce_game");

            // Act 2: a careless load the Skeptic watches.
            yield return Until(() => cupTrick != null && cupTrick.isActiveAndEnabled && cupTrick.Trick.Phase == CupPhase.Idle, 10f, "act 2");
            yield return Wait(1.5f);
            Mark("05_bust_game");
            yield return PickUpAndShow(true);
            LookAt(cupTrick.Coin.Position, 0.4f);
            yield return Wait(0.8f);
            Vector3 cupBase = cupTrick.Cup.Base;
            Vector3 load = cupBase + Vector3.up * 0.05f - TableForward() * 0.05f;
            yield return right.MovePalm(load, Reach(55f), HandPose.Pinch, 1f, 0.2f, 0.9f, this);
            yield return right.SetGrip(0f, 1f, HandPose.Open, 0.25f, this);
            yield return Until(() => cupTrick.Trick.Phase == CupPhase.Loaded || cupTrick.Trick.Phase == CupPhase.Busted, 3f, "load");
            yield return ShowEmpty(cupTrick);
            yield return Wait(3.0f);
            End("05_bust_game");

            // Hands leave view: the crowd waits, then the show carries on.
            Mark("06_pause_game");
            yield return right.MoveWrist(MatWorld(0.18f, 0.06f, -0.25f), flat, 0.6f, this);
            right.Hide();
            yield return Wait(4.0f);
            right.Show(MatWorld(0.18f, 0.06f, -0.25f), flat, HandPose.Open, 0f, 1f);
            left.Show(MatWorld(-0.18f, 0.06f, -0.25f), flat, HandPose.Open, 0f, 1f);
            StartCoroutine(left.MoveWrist(MatWorld(-0.16f, 0.08f, 0.02f), flat, 0.8f, this));
            yield return right.MoveWrist(MatWorld(0.16f, 0.08f, 0.02f), flat, 0.8f, this);
            yield return Wait(3.5f);
            End("06_pause_game");

            Finish();
        }

        private IEnumerator VanishSequence(Spectator target, bool tutorial)
        {
            yield return PickUpAndShow();
            LookAt(target.EyesPosition, 0.45f);
            yield return Until(() => target.Mind.EyeContact, 4f, "eye contact");
            yield return Wait(0.5f);
            yield return Ditch();
            yield return ShowEmpty();
        }

        private IEnumerator PickUpAndShow(bool cup = false)
        {
            CoinActor coin = cup ? cupTrick.Coin : vanish.Coin;
            Vector3 c = coin.Position;
            right.Show(right.PinchPointNow(), Reach(35f), HandPose.Open, 0f, 1f);
            yield return right.MovePinch(c + Vector3.up * 0.09f - TableForward() * 0.03f, Reach(35f), HandPose.Open, 0f, 1f, 0.7f, this);
            yield return right.MovePinch(c + Vector3.up * 0.006f, Reach(35f), HandPose.Open, 0f, 1f, 0.45f, this);
            yield return right.SetGrip(1f, 0.2f, HandPose.Pinch, 0.25f, this);
            yield return Until(() => cup ? cupTrick.Trick.Phase == CupPhase.Showing : vanish.Trick.Phase == TrickPhase.Showing, 3f, "picked up");
            Vector3 up = Vector3.Lerp(c, Eyes, 0.25f) + Vector3.up * 0.22f;
            yield return right.MovePinch(up, Reach(10f), HandPose.Pinch, 1f, 0.2f, 0.8f, this);
            yield return Until(() => cup ? cupTrick.Trick.Phase == CupPhase.Misdirect : vanish.Trick.Phase == TrickPhase.Misdirect, 3f, "shown");
        }

        private IEnumerator Ditch()
        {
            Vector3 edge = MatWorld(0.10f, -0.035f, -0.05f);
            yield return right.MovePalm(edge, Reach(55f), HandPose.Pinch, 1f, 0.2f, 1.0f, this);
            yield return right.SetGrip(0f, 1f, HandPose.Open, 0.25f, this);
            yield return Until(() => vanish.Trick.Phase == TrickPhase.Vanished || vanish.Trick.Phase == TrickPhase.Busted, 3f, "ditched");
        }

        private IEnumerator Steal()
        {
            Vector3 edge = MatWorld(0.10f, -0.04f, -0.03f);
            yield return right.MovePinch(edge, Reach(55f), HandPose.Open, 0f, 1f, 0.9f, this);
            yield return right.SetGrip(1f, 0.25f, HandPose.Pinch, 0.25f, this);
            yield return Until(() => vanish.Trick.Phase == TrickPhase.Loaded, 3f, "stolen");
        }

        private IEnumerator ShowEmpty(CupTrickDirector cup = null)
        {
            Quaternion palmOut = Quaternion.LookRotation(Vector3.up, -TableForward());
            yield return right.MoveWrist(MatWorld(0.10f, 0.12f, 0.12f), palmOut, 0.7f, this);
            yield return Until(() => cup != null
                ? cup.Trick.Phase == CupPhase.Vanished || cup.Trick.Phase == CupPhase.Busted
                : vanish.Trick.Phase == TrickPhase.AfterVanish || vanish.Trick.Phase == TrickPhase.Busted, 4f, "revealed");
        }

        // ------------------------------------------------------------------ helpers

        private void LookAt(Vector3 world, float seconds)
        {
            Vector3 dir = world - Eyes;
            if (dir.sqrMagnitude < 1e-6f) return;
            headFrom = headRot;
            headTo = Quaternion.LookRotation(dir.normalized, Vector3.up);
            headStart = T;
            headDur = seconds;
        }

        private IEnumerator Wait(float seconds)
        {
            int until = frame + Mathf.RoundToInt(seconds * Fps);
            while (frame < until) yield return null;
        }

        private IEnumerator Until(Func<bool> condition, float timeout, string what)
        {
            int until = frame + Mathf.RoundToInt(timeout * Fps);
            while (!SafeCheck(condition) && frame < until) yield return null;
            if (!SafeCheck(condition)) Log("TIMEOUT waiting for " + what);
        }

        private static bool SafeCheck(Func<bool> condition)
        {
            try { return condition(); } catch (Exception) { return false; }
        }

        private Vector3 MatWorld(float x, float y, float z)
        {
            if (table != null && table.Calibrated)
            {
                return table.Frame.ToWorld(new System.Numerics.Vector3(x, y, z)).ToUnity();
            }
            return new Vector3(x, 0.74f + y, 0.30f + z);
        }

        private Vector3 TableForward()
        {
            return table != null && table.Calibrated ? table.Frame.Forward.ToUnity() : Vector3.forward;
        }

        private Quaternion Reach(float downDegrees)
        {
            Vector3 f = TableForward();
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
            return Quaternion.AngleAxis(downDegrees, r) * Quaternion.LookRotation(f, Vector3.up);
        }

        private Spectator Kid() { return Find(Archetype.Kid); }

        private Spectator Find(Archetype a)
        {
            if (crowd == null) return null;
            int i = crowd.IndexOf(a);
            return i >= 0 ? crowd.Spectators[i] : null;
        }

        private Vector3 CrowdCentre()
        {
            if (crowd == null || crowd.Spectators.Count == 0) return Eyes + Vector3.forward;
            Vector3 sum = Vector3.zero;
            foreach (Spectator s in crowd.Spectators) sum += s.EyesPosition;
            return sum / crowd.Spectators.Count - Vector3.up * 0.25f;
        }

        private void BuildRoom()
        {
            Part(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.01f, 1.5f), new Vector3(8f, 0.02f, 8f), new Color(0.28f, 0.21f, 0.16f));
            Part(PrimitiveType.Cube, "Table", new Vector3(0f, 0.365f, 0.65f), new Vector3(1.1f, 0.73f, 0.7f), new Color(0.36f, 0.25f, 0.17f));
            Part(PrimitiveType.Cube, "Back Wall", new Vector3(0f, 1.5f, 4.2f), new Vector3(8f, 3f, 0.1f), new Color(0.52f, 0.49f, 0.45f));
            Part(PrimitiveType.Cube, "Left Wall", new Vector3(-3.2f, 1.5f, 1.5f), new Vector3(0.1f, 3f, 8f), new Color(0.47f, 0.44f, 0.41f));
            Part(PrimitiveType.Cube, "Right Wall", new Vector3(3.2f, 1.5f, 1.5f), new Vector3(0.1f, 3f, 8f), new Color(0.47f, 0.44f, 0.41f));
            GameObject l = new GameObject("Room Light");
            Light light = l.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.93f, 0.84f);
            l.transform.rotation = Quaternion.Euler(55f, 20f, 0f);
            RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.44f);
        }

        private void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "Trailer " + name;
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial = SharedMaterials.Lit(color);
        }

        private void Mark(string name) { open[name] = frame; Log("mark " + name + " start"); }

        private void End(string name)
        {
            int start;
            if (open.TryGetValue(name, out start))
            {
                marks.Add(name + " " + start + " " + frame);
                open.Remove(name);
            }
        }

        private void Log(string message) { Debug.Log("[Trailer] t=" + T.ToString("F2") + " " + message); }

        private void Finish()
        {
            recording = false;
            File.WriteAllText(Path.Combine(outDir, "markers.txt"), string.Join("\n", marks.ToArray()) + "\n");
            Log("done: " + marks.Count + " shots, " + frame + " frames");
            Time.captureFramerate = 0;
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ one scripted hand

        private sealed class HandRig
        {
            private readonly TrackedHand hand;
            private readonly GhostHand visual;
            private readonly ScriptedHandInput input = new ScriptedHandInput();
            private Vector3 pinchPoint;
            private Quaternion rot = Quaternion.identity;
            private HandPose pose = HandPose.Open;

            public HandRig(TrackedHand hand, bool isLeft, Transform parent, Material skin)
            {
                this.hand = hand;
                visual = new GhostHand(parent, isLeft, skin);
                visual.SetActive(false);
                hand.Scripted = input;
            }

            public Vector3 PinchPointNow() { return pinchPoint; }

            public void Hide() { input.Tracked = false; }

            public void Show(Vector3 at, Quaternion rotation, HandPose p, float pinch, float openness)
            {
                pinchPoint = at; rot = rotation; pose = p;
                input.Pinch = pinch; input.Openness = openness; input.ThumbIndexGap = pinch > 0.5f ? 0.01f : 0.06f;
                input.Tracked = true;
            }

            public void Apply()
            {
                input.Wrist = new Pose(pinchPoint - rot * PinchLocal, rot);
                input.PinchPoint = pinchPoint;
                bool visible = input.Tracked || (hand.Presence != null && hand.Presence.HasPose && hand.Presence.State != HandPresenceState.Lost);
                visual.SetActive(input.Tracked);
                if (visible && input.Tracked)
                {
                    Pose w = hand.StableWrist;
                    visual.Place(w.position, w.rotation, pose);
                }
            }

            public IEnumerator MoveWrist(Vector3 wrist, Quaternion rotation, float seconds, TrailerAutopilot owner)
            {
                return MovePinch(wrist + rotation * PinchLocal, rotation, pose, input.Pinch, input.Openness, seconds, owner);
            }

            public IEnumerator MovePalm(Vector3 palm, Quaternion rotation, HandPose p, float pinch, float openness, float seconds, TrailerAutopilot owner)
            {
                Vector3 palmLocal = new Vector3(0f, -0.02f, 0.06f);
                Vector3 wrist = palm - rotation * palmLocal;
                return MovePinch(wrist + rotation * PinchLocal, rotation, p, pinch, openness, seconds, owner);
            }

            public IEnumerator MovePinch(Vector3 target, Quaternion rotation, HandPose p, float pinch, float openness, float seconds, TrailerAutopilot owner)
            {
                Vector3 from = pinchPoint;
                Quaternion fromRot = rot;
                HandPose fromPose = pose;
                float fromPinch = input.Pinch, fromOpen = input.Openness;
                int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
                for (int i = 1; i <= frames; i++)
                {
                    float k = i / (float)frames;
                    k = k * k * (3f - 2f * k);
                    pinchPoint = Vector3.Lerp(from, target, k);
                    rot = Quaternion.Slerp(fromRot, rotation, k);
                    pose = HandPose.Lerp(fromPose, p, k);
                    input.Pinch = Mathf.Lerp(fromPinch, pinch, k);
                    input.Openness = Mathf.Lerp(fromOpen, openness, k);
                    input.ThumbIndexGap = Mathf.Lerp(0.06f, 0.01f, input.Pinch);
                    yield return null;
                }
            }

            public IEnumerator SetGrip(float pinch, float openness, HandPose p, float seconds, TrailerAutopilot owner)
            {
                return MovePinch(pinchPoint, rot, p, pinch, openness, seconds, owner);
            }
        }
    }
}
#endif
