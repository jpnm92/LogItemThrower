using UnityEngine;

namespace LogItemThrower
{
    public class ThrownProjectile : MonoBehaviour
    {
        private static readonly BepInEx.Logging.ManualLogSource _log =
            BepInEx.Logging.Logger.CreateLogSource("LogItemThrower");

        public float damage;
        private Rigidbody rb;
        private bool _hasHit = false;
        private GameObject _hitVfxPrefab;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            _hitVfxPrefab = ZNetScene.instance?.GetPrefab("vfx_clubhit");
            Destroy(this, 10f);
        }

        void FixedUpdate()
        {
            if (_hasHit || rb == null) return;

            // Manual sweep — not dependent on layer collision matrix
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.8f);
            foreach (var col in hits)
            {
                Character character = col.GetComponentInParent<Character>();
                if (character == null || character == Player.m_localPlayer) continue;

                _hasHit = true;

                HitData hit = new HitData();
                hit.m_damage.m_blunt = damage;
                hit.m_point = col.ClosestPoint(transform.position);
                hit.m_dir = rb.linearVelocity.normalized;
                hit.m_skill = Skills.SkillType.Clubs;
                hit.m_pushForce = LogItemThrower.LogPushForce.Value;
                hit.m_attacker = Player.m_localPlayer.GetZDOID();
                character.Damage(hit);

                _log.LogInfo($"Hit {character.name} for {hit.m_damage.m_blunt}");

                if (DamageText.instance != null)
                    DamageText.instance.ShowText(DamageText.TextType.Normal, hit.m_point, hit.m_damage.m_blunt, false);

                if (_hitVfxPrefab != null)
                    Instantiate(_hitVfxPrefab, hit.m_point, Quaternion.identity);

                ApplyAoe(hit.m_point, damage, character);
                Destroy(this);
                return;
            }
        }

        private void ApplyAoe(Vector3 point, float directDamage, Character directHit)
        {
            Collider[] nearby = Physics.OverlapSphere(point, LogItemThrower.LogAoeRadius.Value);
            foreach (var col in nearby)
            {
                Character aoeTarget = col.GetComponentInParent<Character>();
                if (aoeTarget == null) continue;
                if (aoeTarget == Player.m_localPlayer) continue;
                if (aoeTarget == directHit) continue;

                HitData aoeHit = new HitData();
                aoeHit.m_damage.m_blunt = directDamage * LogItemThrower.LogAoeDamageMultiplier.Value;
                aoeHit.m_point = point;
                aoeHit.m_dir = (aoeTarget.transform.position - point).normalized;
                aoeHit.m_skill = Skills.SkillType.Clubs;
                aoeHit.m_pushForce = LogItemThrower.LogPushForce.Value * 0.5f;
                aoeHit.m_attacker = Player.m_localPlayer.GetZDOID();
                aoeTarget.Damage(aoeHit);

                if (DamageText.instance != null)
                    DamageText.instance.ShowText(DamageText.TextType.Normal, aoeHit.m_point, aoeHit.m_damage.m_blunt, false);
                if (_hitVfxPrefab != null)
                    Instantiate(_hitVfxPrefab, point, Quaternion.identity);
            }
        }
    }
}