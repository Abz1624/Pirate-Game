using HighSeas.Combat;
using HighSeas.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.Enemies
{
    /// <summary>Reef Crab miniboss: closes to shooting range, telegraphed bubble shot, repeat.</summary>
    public static class ReefCrabMinibossBehavior
    {
        const string Id = "reef_crab_miniboss";
        const float GroundY = 0.38f;
        const float PreferredShootRange = 12f;
        const float ShootWindupSeconds = 1.1f;
        const float ShootRecoverSeconds = 1.25f;

        enum Phase
        {
            Pursue,
            ShootWindup,
            ShootRecover
        }

        sealed class State
        {
            public Phase phase;
            public float timer;
            public Vector3 aimDir;
            public Transform telegraphRoot;
            public MeshFilter coneFilter;
            public MeshRenderer coneRenderer;
            public Material material;
        }

        static readonly System.Collections.Generic.Dictionary<int, State> States = new();

        public static bool Handles(Data.EnemyDefinition def) =>
            def != null && def.id == Id;

        public static void ClearAgent(int agentUnitId)
        {
            if (!States.TryGetValue(agentUnitId, out var state))
                return;
            DestroyTelegraph(state);
            States.Remove(agentUnitId);
        }

        public static Vector3 TickMovement(
            int agentIndex,
            ref EnemyAgent agent,
            Data.EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            var state = GetOrCreate(agent.id);
            TickPhase(ref agent, state, def, dt, playerPos);
            UpdateTelegraph(ref agent, state);

            switch (state.phase)
            {
                case Phase.ShootWindup:
                    return Vector3.zero;
                case Phase.ShootRecover:
                    return MoveToward(agent, playerPos, def.moveSpeed * 0.35f);
                default:
                {
                    var to = playerPos - agent.position;
                    to.y = 0f;
                    var dist = to.magnitude;
                    if (dist > PreferredShootRange + 1.5f)
                        return MoveToward(agent, playerPos, def.moveSpeed * 0.9f);
                    if (dist < PreferredShootRange - 3f)
                        return -to.normalized * (def.moveSpeed * 0.45f);
                    return MoveToward(agent, playerPos, def.moveSpeed * 0.25f);
                }
            }
        }

        static void TickPhase(
            ref EnemyAgent agent,
            State state,
            Data.EnemyDefinition def,
            float dt,
            Vector3 playerPos)
        {
            state.timer -= dt;
            var toPlayer = playerPos - agent.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            state.aimDir = dist > 0.01f ? toPlayer / dist : Vector3.forward;

            switch (state.phase)
            {
                case Phase.Pursue:
                    if (dist <= PreferredShootRange + 0.5f && dist >= PreferredShootRange - 4f)
                    {
                        state.phase = Phase.ShootWindup;
                        state.timer = ShootWindupSeconds;
                    }
                    break;
                case Phase.ShootWindup:
                    if (state.timer <= 0f)
                    {
                        FireBubble(ref agent, state, def, playerPos);
                        state.phase = Phase.ShootRecover;
                        state.timer = ShootRecoverSeconds;
                        HideTelegraph(state);
                    }
                    break;
                case Phase.ShootRecover:
                    if (state.timer <= 0f)
                        state.phase = Phase.Pursue;
                    break;
            }
        }

        static void FireBubble(ref EnemyAgent agent, State state, Data.EnemyDefinition def, Vector3 playerPos)
        {
            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles == null) return;

            var dir = state.aimDir;
            if (dir.sqrMagnitude < 0.01f)
                dir = (playerPos - agent.position).normalized;
            dir.y = 0f;
            dir.Normalize();
            var muzzle = agent.position + dir * (def.radius + 0.65f) + Vector3.up * 0.55f;
            hostiles.TrySpawn(muzzle, dir, 15f, def.baseDamage, 2.5f, 0.55f, 0.65f);
        }

        static Vector3 MoveToward(in EnemyAgent agent, Vector3 playerPos, float speed)
        {
            var to = playerPos - agent.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f) return Vector3.zero;
            return to.normalized * speed;
        }

        static void UpdateTelegraph(ref EnemyAgent agent, State state)
        {
            if (state.phase != Phase.ShootWindup)
            {
                HideTelegraph(state);
                return;
            }

            EnsureTelegraph(state, agent.view);
            var t = 1f - Mathf.Clamp01(state.timer / ShootWindupSeconds);
            BuildFan(state.coneFilter.sharedMesh, agent.position, state.aimDir, 17f, 24f);
            SetTelegraphColor(state.coneRenderer, state.material, new Color(0.25f, 0.85f, 1f, 0.14f + t * 0.48f));
            state.coneFilter.gameObject.SetActive(true);
        }

        static void HideTelegraph(State state)
        {
            if (state?.coneFilter != null)
                state.coneFilter.gameObject.SetActive(false);
        }

        static State GetOrCreate(int agentUnitId)
        {
            if (!States.TryGetValue(agentUnitId, out var state))
            {
                state = new State { phase = Phase.Pursue, timer = 0.5f };
                States[agentUnitId] = state;
            }

            return state;
        }

        static void EnsureTelegraph(State state, Transform parent)
        {
            if (state.telegraphRoot != null) return;

            state.material = CreateTelegraphMaterial();
            state.telegraphRoot = new GameObject("ReefCrabTelegraphs").transform;
            if (parent != null)
                state.telegraphRoot.SetParent(parent, false);

            var coneGo = new GameObject("SpitCone");
            coneGo.transform.SetParent(state.telegraphRoot, false);
            state.coneFilter = coneGo.AddComponent<MeshFilter>();
            state.coneFilter.sharedMesh = new Mesh { name = "SpitConeMesh" };
            state.coneRenderer = coneGo.AddComponent<MeshRenderer>();
            state.coneRenderer.sharedMaterial = state.material;
            state.coneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            HideTelegraph(state);
        }

        static void DestroyTelegraph(State state)
        {
            if (state == null) return;
            if (state.telegraphRoot != null)
                Object.Destroy(state.telegraphRoot.gameObject);
            if (state.material != null)
                Object.Destroy(state.material);
        }

        static void SetTelegraphColor(MeshRenderer renderer, Material mat, Color color)
        {
            if (renderer == null || mat == null) return;
            mat.color = color;
        }

        static Material CreateTelegraphMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3099;
            return mat;
        }

        static void BuildFan(Mesh mesh, Vector3 origin, Vector3 forward, float range, float arcDegrees)
        {
            if (mesh == null) return;
            mesh.Clear();
            origin.y = GroundY;
            const int segments = 20;
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = origin;
            var half = arcDegrees * 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(-half, half, t);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward.normalized;
                verts[i + 1] = origin + dir * range;
                verts[i + 1].y = GroundY;
            }

            var tri = 0;
            for (var i = 0; i < segments; i++)
            {
                tris[tri++] = 0;
                tris[tri++] = i + 1;
                tris[tri++] = i + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }
    }
}
