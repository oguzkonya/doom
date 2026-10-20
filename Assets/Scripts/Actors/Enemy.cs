using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Monster AI modelled on the original: sleeps until it sees or hears the player, chases
    /// (direct when in sight, flow-field pathing otherwise), attacks with distance-weighted odds,
    /// flinches on pain chance, retaliates against other monsters that hurt it (infighting),
    /// opens unlocked doors, and on Nightmare respawns after a while.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        enum State { Idle, Chase, Attack, Charge, Pain, Dead }
        enum Frame { Walk0, Walk1, Attack0, Attack1, Pain, Death0, Death1, Death2 }

        public EnemyDef Def { get; private set; }
        public bool IsDead => state == State.Dead;
        public bool Bleeds => Def.Id != "lostsoul";
        public Vector3 Center => transform.position + Vector3.up * Def.Height * 0.55f;
        Vector3 Eye => transform.position + Vector3.up * Def.Height * 0.75f;

        CharacterController cc;
        Billboard sprite;
        string[] frameNames;
        State state;
        int health;
        GameObject target;
        IDamageable targetDamageable;
        Vector3 facing, avoidDir, knock, chargeVel, spawnPos, lastPos;
        float spawnYaw, thinkTimer, stateTimer, attackCooldown, animTimer, stuckTimer, avoidTimer, zigTimer, zigAngle;
        float vy, deathTimer, respawnTimer = -1f;
        bool attackFired, meleeAttack, hasLineOfSight;
        Vector2Int teleportCell = new(-1, -1);
        int walkFrame;

        static int Skill => GameManager.I != null ? GameManager.I.Skill : 3;
        static bool Fast => Skill >= 5;

        public static Enemy Spawn(EnemyDef def, Transform parent, Vector3 position, float yaw)
        {
            var go = new GameObject(def.Id) { layer = Layers.Actor };
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.radius = def.Radius;
            cc.height = def.Height;
            cc.center = new Vector3(0, def.Height * 0.5f, 0);
            cc.stepOffset = def.Flying ? 0.1f : 0.55f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;
            var e = go.AddComponent<Enemy>();
            e.Def = def;
            e.cc = cc;
            e.spawnPos = position;
            e.spawnYaw = yaw;
            e.sprite = Billboard.Create(go.transform, false, def.Fullbright);
            e.frameNames = new string[PlaceholderArt.MonsterFrames.Length];
            for (int i = 0; i < e.frameNames.Length; i++) e.frameNames[i] = def.Id + "_" + PlaceholderArt.MonsterFrames[i];
            e.ResetState();
            return e;
        }

        void ResetState()
        {
            health = Def.Health;
            state = State.Idle;
            facing = Quaternion.Euler(0, spawnYaw, 0) * Vector3.forward;
            target = null;
            targetDamageable = null;
            cc.enabled = true;
            sprite.SetVisible(true);
            SetFrame(Frame.Walk0);
            lastPos = transform.position;
        }

        void SetFrame(Frame f) => sprite.SetSprite(frameNames[(int)f]);

        void PlaySound(string kind) =>
            AudioManager.I.PlayAt(GameAssets.Sound(Def.Id + "_" + kind, "monster_" + kind), transform.position, 1f, Def.SoundPitch);

        // ------------------------------------------------------------------ targeting

        static PlayerController Player => GameManager.I != null ? GameManager.I.Player : null;

        void SetTarget(GameObject t)
        {
            target = t;
            targetDamageable = t != null ? t.GetComponent<IDamageable>() : null;
        }

        bool TargetValid => target != null && targetDamageable != null && !targetDamageable.IsDead;

        Vector3 TargetCenter
        {
            get
            {
                if (target.TryGetComponent<PlayerController>(out var pc)) return pc.Center;
                if (target.TryGetComponent<Enemy>(out var e)) return e.Center;
                return target.transform.position;
            }
        }

        float TargetRadius => target.TryGetComponent<Enemy>(out var e) ? e.Def.Radius : PlayerController.Radius;

        bool CanSeePlayer(bool useFieldOfView)
        {
            var p = Player;
            if (p == null || p.Status.IsDead) return false;
            var to = p.Center - Eye;
            float dist = to.magnitude;
            if (dist > 60f) return false;
            if (useFieldOfView && dist > 4f)
            {
                var flat = new Vector3(to.x, 0, to.z).normalized;
                if (Vector3.Dot(facing, flat) < 0f) return false;
            }
            return Combat.LineOfSight(Eye, p.EyePosition);
        }

        /// <summary>Called when the player makes noise nearby.</summary>
        public void Alert(GameObject source)
        {
            if (state == State.Idle && Player != null) Wake(Player.gameObject);
        }

        void Wake(GameObject t)
        {
            SetTarget(t);
            state = State.Chase;
            attackCooldown = Random.Range(0.3f, 0.8f);
            PlaySound("sight");
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (state == State.Dead) { UpdateDead(dt); return; }

            attackCooldown -= dt;
            if (state != State.Idle && !TargetValid)
            {
                var p = Player;
                if (p != null && !p.Status.IsDead && target != p.gameObject) SetTarget(p.gameObject);
                else if (p == null || p.Status.IsDead) { state = State.Idle; SetTarget(null); }
            }

            switch (state)
            {
                case State.Idle: UpdateIdle(dt); break;
                case State.Chase: UpdateChase(dt); break;
                case State.Attack: UpdateAttack(dt); break;
                case State.Charge: UpdateCharge(dt); break;
                case State.Pain:
                    Move(Vector3.zero, dt);
                    stateTimer -= dt;
                    if (stateTimer <= 0f) state = State.Chase;
                    break;
            }
        }

        void UpdateIdle(float dt)
        {
            Move(Vector3.zero, dt);
            thinkTimer -= dt;
            if (thinkTimer > 0f) return;
            thinkTimer = 0.3f;
            if (CanSeePlayer(true)) Wake(Player.gameObject);
        }

        void UpdateChase(float dt)
        {
            if (!TargetValid) { Move(Vector3.zero, dt); return; }

            var toTarget = TargetCenter - Center;
            var flatTo = new Vector3(toTarget.x, 0, toTarget.z);
            float dist = flatTo.magnitude;

            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.15f;
                hasLineOfSight = Combat.LineOfSight(Eye, TargetCenter + Vector3.up * 0.3f);
                if (attackCooldown <= 0f && hasLineOfSight && TryAttack(dist)) return;
                if (Random.value < 0.015f) PlaySound("active");
            }

            bool targetIsPlayer = Player != null && target == Player.gameObject;
            Vector3 dir;
            if (avoidTimer > 0f)
            {
                avoidTimer -= dt;
                dir = avoidDir;
            }
            else if (hasLineOfSight || !targetIsPlayer)
            {
                dir = dist > 0.01f ? flatTo / dist : facing;
                zigTimer -= dt;
                if (zigTimer <= 0f)
                {
                    zigTimer = Random.Range(0.5f, 1.2f);
                    zigAngle = dist > 5f ? Random.Range(-35f, 35f) : 0f;
                }
                dir = Quaternion.Euler(0, zigAngle, 0) * dir;
            }
            else
            {
                var level = GameManager.I.Level;
                dir = level != null ? level.FlowDirection(transform.position) : Vector3.zero;
                if (dir == Vector3.zero) dir = dist > 0.01f ? flatTo / dist : facing;
            }

            // Don't push into the target when already in melee reach.
            bool inReach = dist < Def.Radius + TargetRadius + 0.4f;
            var move = inReach ? Vector3.zero : dir * Def.Speed * (Fast ? 1.4f : 1f);
            Move(move, dt);
            if (dir != Vector3.zero) facing = dir;
            var currentLevel = GameManager.I.Level;
            if (currentLevel != null && currentLevel.TryTeleport(gameObject, transform.position, ref teleportCell)) return;

            // Stuck detection: sidestep and try to open doors.
            var moved = transform.position - lastPos;
            moved.y = 0;
            lastPos = transform.position;
            if (move != Vector3.zero && moved.magnitude < move.magnitude * dt * 0.3f) stuckTimer += dt;
            else stuckTimer = Mathf.Max(0f, stuckTimer - dt);
            if (stuckTimer > 0.3f)
            {
                stuckTimer = 0f;
                TryOpenDoor(dir);
                avoidDir = Quaternion.Euler(0, Random.value < 0.5f ? 90f : -90f, 0) * dir;
                avoidTimer = Random.Range(0.3f, 0.7f);
            }

            animTimer += dt;
            if (animTimer > 0.2f)
            {
                animTimer = 0f;
                walkFrame ^= 1;
            }
            SetFrame(walkFrame == 0 ? Frame.Walk0 : Frame.Walk1);
        }

        void TryOpenDoor(Vector3 dir)
        {
            if (Combat.Trace(Center, dir, Def.Radius + 1.2f, gameObject, Layers.WorldMask, out var hit) &&
                hit.collider.TryGetComponent<Door>(out var door))
                door.MonsterBump();
        }

        bool TryAttack(float dist)
        {
            bool inMelee = dist < Def.Radius + TargetRadius + 1.0f;
            if (Def.Melee && inMelee)
            {
                StartAttack(true);
                return true;
            }
            if (Def.Ranged == AttackKind.None || dist > 50f) return false;
            float chance = Mathf.Clamp(1.1f - dist / 18f, 0.15f, 0.9f);
            if (Fast) chance = Mathf.Min(1f, chance * 1.4f);
            if (Random.value >= chance) return false;
            StartAttack(false);
            return true;
        }

        void StartAttack(bool melee)
        {
            state = State.Attack;
            meleeAttack = melee;
            attackFired = false;
            stateTimer = Def.Windup * (Fast ? 0.5f : 1f);
            FaceTarget();
            SetFrame(Frame.Attack0);
        }

        void FaceTarget()
        {
            if (!TargetValid) return;
            var d = TargetCenter - Center;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) facing = d.normalized;
        }

        void UpdateAttack(float dt)
        {
            Move(Vector3.zero, dt);
            stateTimer -= dt;
            if (!attackFired)
            {
                FaceTarget();
                if (stateTimer > 0f) return;
                attackFired = true;
                stateTimer = 0.25f;
                SetFrame(Frame.Attack1);
                FireAttack();
                return;
            }
            if (stateTimer <= 0f && state == State.Attack) EndAttack();
        }

        void EndAttack()
        {
            state = State.Chase;
            float skillScale = Skill <= 2 ? 1.3f : Fast ? 0.5f : 1f;
            attackCooldown = Def.Cooldown * Random.Range(0.6f, 1.4f) * skillScale;
        }

        void FireAttack()
        {
            if (!TargetValid) return;
            var tc = TargetCenter;
            var dir = (tc - Eye).normalized;

            if (meleeAttack)
            {
                AudioManager.I.PlayAt(Def.MeleeSound, transform.position);
                var flat = tc - Center;
                flat.y = 0;
                if (flat.magnitude < Def.Radius + TargetRadius + 1.1f)
                    targetDamageable.Damage(Combat.Roll(Def.MeleeSides, Def.MeleeMultiplier), dir, gameObject);
                return;
            }

            switch (Def.Ranged)
            {
                case AttackKind.Hitscan:
                    AudioManager.I.PlayAt(Def.AttackSound, transform.position);
                    for (int i = 0; i < Def.Pellets; i++)
                    {
                        var d = Quaternion.AngleAxis(Combat.Spread(22.4f), Vector3.up) * dir;
                        Combat.FireBullet(Eye, d, 64f, Combat.Roll(5, 3), gameObject);
                    }
                    break;
                case AttackKind.Projectile:
                    AudioManager.I.PlayAt(Def.AttackSound, transform.position);
                    Projectile.Spawn(Def.Projectile, Eye, dir, gameObject, Fast ? 1.5f : 1f);
                    break;
                case AttackKind.Charge:
                    PlaySound("attack");
                    state = State.Charge;
                    stateTimer = 1f;
                    chargeVel = (tc - Center).normalized * 21.9f;
                    break;
            }
        }

        void UpdateCharge(float dt)
        {
            stateTimer -= dt;
            var flags = cc.Move(chargeVel * dt);
            if (TargetValid && Vector3.Distance(TargetCenter, Center) < Def.Radius + TargetRadius + 0.3f)
            {
                targetDamageable.Damage(Combat.Roll(Def.MeleeSides, Def.MeleeMultiplier), chargeVel.normalized, gameObject);
                EndAttack();
                SetFrame(Frame.Walk0);
                return;
            }
            if (flags != CollisionFlags.None || stateTimer <= 0f)
            {
                EndAttack();
                SetFrame(Frame.Walk0);
            }
        }

        void Move(Vector3 horizontal, float dt)
        {
            if (!cc.enabled) return;
            knock *= Mathf.Exp(-6f * dt);

            if (Def.Flying)
            {
                vy = 0f;
                if (state != State.Idle && TargetValid)
                {
                    float wanted = TargetCenter.y - Center.y;
                    vy = Mathf.Clamp(wanted * 2f, -Def.Speed, Def.Speed);
                }
            }
            else
            {
                if (cc.isGrounded && vy < 0f) vy = -2f;
                vy -= 22f * dt;
            }

            cc.Move((horizontal + knock + Vector3.up * vy) * dt);
        }

        /// <summary>Carried by a moving lift.</summary>
        public void Ride(float dy)
        {
            if (cc.enabled) cc.Move(Vector3.up * dy);
        }

        /// <summary>Instant relocation (teleporters).</summary>
        public void Warp(Vector3 position, float yaw)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            lastPos = position;
            facing = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            avoidTimer = 0f;
            stuckTimer = 0f;
        }

        // ------------------------------------------------------------------ save / load

        public EnemySave Capture() => new()
        {
            id = Def.Id,
            position = transform.position,
            spawnPosition = spawnPos,
            spawnYaw = spawnYaw,
            facing = facing,
            health = health,
            dead = IsDead,
            awake = !IsDead && state != State.Idle,
            respawnTimer = respawnTimer
        };

        public static Enemy Restore(EnemySave s, Transform parent)
        {
            if (!EnemyDefs.TryGet(s.id, out var def))
            {
                Debug.LogWarning($"[Doom] Save references unknown monster '{s.id}'");
                return null;
            }
            var e = Spawn(def, parent, s.spawnPosition, s.spawnYaw);
            e.cc.enabled = false;
            e.transform.position = s.position;
            e.lastPos = s.position;
            if (GameManager.I != null && GameManager.I.Level != null) e.teleportCell = GameManager.I.Level.WorldToCell(s.position);
            e.health = s.health;
            if (s.facing != Vector3.zero) e.facing = s.facing;

            if (s.dead)
            {
                e.state = State.Dead;
                e.deathTimer = 1f;
                e.respawnTimer = s.respawnTimer;
                e.SetFrame(Frame.Death2);
                if (def.Id == "lostsoul") e.sprite.SetVisible(false);
                return e;
            }

            e.cc.enabled = true;
            if (s.awake && Player != null && !Player.Status.IsDead)
            {
                e.SetTarget(Player.gameObject);
                e.state = State.Chase;
            }
            return e;
        }

        // ------------------------------------------------------------------ damage & death

        public void Damage(int amount, Vector3 direction, GameObject source)
        {
            if (IsDead) return;
            health -= amount;

            var push = new Vector3(direction.x, 0, direction.z);
            if (push.sqrMagnitude > 0.0001f)
                knock += push.normalized * Mathf.Min(amount * 12f / Def.Mass * 1.1f, 10f);

            if (source != null && source != gameObject)
            {
                if (source.TryGetComponent<Enemy>(out var other))
                {
                    if (!other.IsDead) SetTarget(source); // infighting
                }
                else if (source.TryGetComponent<PlayerController>(out _) && (target == null || state == State.Idle))
                    SetTarget(source);
            }

            if (health <= 0) { Die(); return; }

            if (state == State.Idle)
            {
                var t = target != null ? target : Player != null ? Player.gameObject : null;
                if (t != null) Wake(t);
            }

            if (Random.Range(0, 256) < Def.PainChance)
            {
                state = State.Pain;
                stateTimer = Def.PainTime;
                SetFrame(Frame.Pain);
                PlaySound("pain");
            }
        }

        void Die()
        {
            state = State.Dead;
            cc.enabled = false;
            deathTimer = 0f;
            SetFrame(Frame.Death0);
            PlaySound("death");
            GameManager.I.OnKill();
            if (Def.Drop.HasValue && GameManager.I.Level != null)
                Pickup.Spawn(Def.Drop.Value, GameManager.I.Level.transform, transform.position + Vector3.up * 0.01f, true);
            respawnTimer = Skill >= 5 && Def.Id != "lostsoul" ? Random.Range(12f, 20f) : -1f;
        }

        void UpdateDead(float dt)
        {
            deathTimer += dt;
            int f = Mathf.Min(2, (int)(deathTimer / 0.15f));
            SetFrame(Frame.Death0 + f);
            if (Def.Id == "lostsoul" && deathTimer > 0.45f) sprite.SetVisible(false);

            // Flying corpses fall to the floor.
            var level = GameManager.I.Level;
            if (level != null)
            {
                float floor = level.CellAt(transform.position).Floor;
                var p = transform.position;
                if (p.y > floor) transform.position = new Vector3(p.x, Mathf.MoveTowards(p.y, floor, 8f * dt), p.z);
            }

            if (respawnTimer > 0f)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f)
                {
                    bool blocked = Physics.CheckCapsule(spawnPos + Vector3.up * Def.Radius,
                        spawnPos + Vector3.up * (Def.Height - Def.Radius), Def.Radius, Layers.ActorMask, QueryTriggerInteraction.Ignore);
                    if (blocked) respawnTimer = 2f;
                    else
                    {
                        transform.position = spawnPos;
                        ResetState();
                        Effect.Spawn("fx_bfgspray_", 3, 0.12f, Center);
                    }
                }
            }
        }
    }
}
