using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class CameraPlayerSync : MonoBehaviour
    {
        const float WorldSyncRate = 1f / 30f;

        [System.Serializable] private class PlayerPos        { public float x, y; }
        [System.Serializable] private class PlatformState   { public float[] x, y; }
        [System.Serializable] private class CollectibleState { public int[] active; }
        [System.Serializable] private class GoalState       { public int[] unlocked; }
        [System.Serializable] private class StageSync       { public int index; }

        // ── Host ────────────────────────────────────────────────────────────
        private Transform[]   hostPlatforms;
        private Collectible[] hostCollectibles;
        private Stage[]       hostStages;
        private float         worldSyncTimer;
        private GameObject    cachedPlayer;

        // ── Camera ──────────────────────────────────────────────────────────
        private GameObject    ghostPlayer;
        private Transform[]   ghostPlatforms;
        private Collectible[] ghostCollectibles;
        private Stage[]       ghostStages;

        void Start()
        {
            if (AppBootstrap.CameraViewIndex >= 0)
                SetupCameraMode();
            else
                SetupHostMode();
        }

        // ───────────────────────────────── HOST ──────────────────────────────

        private void SetupHostMode()
        {
            hostPlatforms    = GatherPlatformTransforms();
            hostCollectibles = GatherCollectibles();
            hostStages       = GatherStages();

            if (AppBootstrap.CameraBridges != null)
                foreach (var bridge in AppBootstrap.CameraBridges)
                    if (bridge != null)
                        bridge.OnConnected.AddListener(SendStageSync);
        }

        public void SendStageSync()
        {
            if (AppBootstrap.CameraBridges == null) return;
            int idx = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0;
            SendToAll(new BridgeMessage {
                type    = "stage-sync",
                payload = JsonUtility.ToJson(new StageSync { index = idx })
            });
        }

        void Update()
        {
            if (AppBootstrap.CameraViewIndex >= 0 || AppBootstrap.CameraBridges == null) return;
            if (!AnyBridgeConnected()) return;

            var player = GetActivePlayer();
            if (player != null)
                SendToAll(new BridgeMessage {
                    type    = "player-pos",
                    payload = JsonUtility.ToJson(new PlayerPos {
                        x = player.transform.position.x,
                        y = player.transform.position.y
                    })
                });

            worldSyncTimer -= Time.deltaTime;
            if (worldSyncTimer > 0f) return;
            worldSyncTimer = WorldSyncRate;

            SendPlatformState();
            SendCollectibleState();
            SendGoalState();
        }

        private void SendPlatformState()
        {
            if (hostPlatforms == null || hostPlatforms.Length == 0) return;

            var ps = new PlatformState {
                x = new float[hostPlatforms.Length],
                y = new float[hostPlatforms.Length]
            };
            for (int i = 0; i < hostPlatforms.Length; i++)
            {
                if (hostPlatforms[i] == null) continue;
                ps.x[i] = hostPlatforms[i].position.x;
                ps.y[i] = hostPlatforms[i].position.y;
            }
            SendToAll(new BridgeMessage { type = "platform-state", payload = JsonUtility.ToJson(ps) });
        }

        private void SendCollectibleState()
        {
            if (hostCollectibles == null || hostCollectibles.Length == 0) return;

            var cs = new CollectibleState { active = new int[hostCollectibles.Length] };
            for (int i = 0; i < hostCollectibles.Length; i++)
                if (hostCollectibles[i] != null)
                    cs.active[i] = hostCollectibles[i].gameObject.activeSelf ? 1 : 0;
            SendToAll(new BridgeMessage { type = "collectible-state", payload = JsonUtility.ToJson(cs) });
        }

        private void SendGoalState()
        {
            if (hostStages == null || hostStages.Length == 0) return;

            var gs = new GoalState { unlocked = new int[hostStages.Length] };
            for (int i = 0; i < hostStages.Length; i++)
                if (hostStages[i] != null)
                    gs.unlocked[i] = hostStages[i].IsGoalUnlocked ? 1 : 0;
            SendToAll(new BridgeMessage { type = "goal-state", payload = JsonUtility.ToJson(gs) });
        }

        private bool AnyBridgeConnected()
        {
            foreach (var b in AppBootstrap.CameraBridges)
                if (b != null && b.IsConnected) return true;
            return false;
        }

        private void SendToAll(BridgeMessage msg)
        {
            foreach (var bridge in AppBootstrap.CameraBridges)
                if (bridge != null && bridge.IsConnected)
                    bridge.Send(msg);
        }

        // ─────────────────────────────── CAMERA ──────────────────────────────

        private void SetupCameraMode()
        {
            ghostPlayer = GameObject.FindGameObjectWithTag("Player");
            if (ghostPlayer != null)
            {
                var ctrl = ghostPlayer.GetComponent<PlayerController2D>();
                if (ctrl != null) ctrl.enabled = false;
                var rb = ghostPlayer.GetComponent<Rigidbody2D>();
                if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
                foreach (var r in ghostPlayer.GetComponentsInChildren<Renderer>())
                    r.enabled = false;
            }

            foreach (var p in Object.FindObjectsByType<MovingPlatform>  (FindObjectsInactive.Include, FindObjectsSortMode.None))
                p.enabled = false;
            foreach (var p in Object.FindObjectsByType<OrbitingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                p.enabled = false;

            ghostPlatforms    = GatherPlatformTransforms();
            ghostCollectibles = GatherCollectibles();
            ghostStages       = GatherStages();

            var bridge = AppBootstrap.CameraClientBridge;
            if (bridge != null)
                bridge.OnMessageReceived.AddListener(OnMessage);
        }

        private void OnMessage(BridgeMessage msg)
        {
            switch (msg.type)
            {
                case "player-pos":
                {
                    var p = JsonUtility.FromJson<PlayerPos>(msg.payload);
                    if (ghostPlayer == null)
                        ghostPlayer = GameObject.FindGameObjectWithTag("Player");
                    if (ghostPlayer != null)
                    {
                        ghostPlayer.transform.position = new Vector3(p.x, p.y, ghostPlayer.transform.position.z);
                        foreach (var r in ghostPlayer.GetComponentsInChildren<Renderer>())
                            r.enabled = true;
                    }
                    break;
                }
                case "platform-state":
                {
                    if (ghostPlatforms == null) break;
                    var ps  = JsonUtility.FromJson<PlatformState>(msg.payload);
                    int len = Mathf.Min(ps.x?.Length ?? 0, ghostPlatforms.Length);
                    for (int i = 0; i < len; i++)
                        if (ghostPlatforms[i] != null)
                            ghostPlatforms[i].position = new Vector3(ps.x[i], ps.y[i], ghostPlatforms[i].position.z);
                    break;
                }
                case "collectible-state":
                {
                    if (ghostCollectibles == null) break;
                    var cs  = JsonUtility.FromJson<CollectibleState>(msg.payload);
                    int len = Mathf.Min(cs.active?.Length ?? 0, ghostCollectibles.Length);
                    for (int i = 0; i < len; i++)
                        if (ghostCollectibles[i] != null)
                            ghostCollectibles[i].gameObject.SetActive(cs.active[i] == 1);
                    break;
                }
                case "goal-state":
                {
                    if (ghostStages == null) break;
                    var gs  = JsonUtility.FromJson<GoalState>(msg.payload);
                    int len = Mathf.Min(gs.unlocked?.Length ?? 0, ghostStages.Length);
                    for (int i = 0; i < len; i++)
                        if (ghostStages[i] != null)
                        {
                            if (gs.unlocked[i] == 1) ghostStages[i].UnlockGoal();
                            else                     ghostStages[i].LockGoal();
                        }
                    break;
                }
                case "stage-sync":
                {
                    var ss = JsonUtility.FromJson<StageSync>(msg.payload);
                    GameManager.Instance?.ForceActiveStage(ss.index);

                    // Disable platform scripts on newly active stage's platforms
                    foreach (var p in Object.FindObjectsByType<MovingPlatform>  (FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        p.enabled = false;
                    foreach (var p in Object.FindObjectsByType<OrbitingPlatform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        p.enabled = false;

                    // Activate the new stage's player so it exists in the scene
                    var allStages = Object.FindObjectsByType<Stage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var stage in allStages)
                        if (stage.gameObject.activeInHierarchy) { stage.ActivatePlayer(); break; }

                    // Re-acquire ghostPlayer — old reference may point to a now-inactive object
                    ghostPlayer = null;
                    var newPlayer = GameObject.FindGameObjectWithTag("Player");
                    if (newPlayer != null)
                    {
                        ghostPlayer = newPlayer;
                        var ctrl = newPlayer.GetComponent<PlayerController2D>();
                        if (ctrl != null) ctrl.enabled = false;
                        var rb = newPlayer.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
                        foreach (var r in newPlayer.GetComponentsInChildren<Renderer>())
                            r.enabled = false;
                    }

                    ghostPlatforms    = GatherPlatformTransforms();
                    ghostCollectibles = GatherCollectibles();
                    ghostStages       = GatherStages();
                    break;
                }
            }
        }

        // ──────────────────────────── Utilidades ─────────────────────────────

        private GameObject GetActivePlayer()
        {
            if (cachedPlayer != null && cachedPlayer.activeInHierarchy) return cachedPlayer;
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
            return cachedPlayer;
        }

        private static Transform[] GatherPlatformTransforms()
        {
            var moving   = Object.FindObjectsByType<MovingPlatform>  (FindObjectsInactive.Include, FindObjectsSortMode.None);
            var orbiting = Object.FindObjectsByType<OrbitingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            System.Array.Sort(moving,   (a, b) => GetPath(a.transform).CompareTo(GetPath(b.transform)));
            System.Array.Sort(orbiting, (a, b) => GetPath(a.transform).CompareTo(GetPath(b.transform)));

            var result = new Transform[moving.Length + orbiting.Length];
            for (int i = 0; i < moving.Length;   i++) result[i]                = moving[i].transform;
            for (int i = 0; i < orbiting.Length; i++) result[moving.Length + i] = orbiting[i].transform;
            return result;
        }

        private static Collectible[] GatherCollectibles()
        {
            var arr = Object.FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(arr, (a, b) => GetPath(a.transform).CompareTo(GetPath(b.transform)));
            return arr;
        }

        private static Stage[] GatherStages()
        {
            var arr = Object.FindObjectsByType<Stage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(arr, (a, b) => GetPath(a.transform).CompareTo(GetPath(b.transform)));
            return arr;
        }

        private static string GetPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            var p  = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }
    }
}
