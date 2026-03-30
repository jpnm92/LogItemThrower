using UnityEngine;

namespace LogItemThrower
{
    public class ThrownProjectile : MonoBehaviour
    {
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

        void OnCollisionEnter(Collision collision)
        {
            if (_hasHit) return;

            Character character = collision.gameObject.GetComponentInParent<Character>();
            if (character == null || character == Player.m_localPlayer) return;

            _hasHit = true;

            HitData hit = new HitData();
            hit.m_damage.m_blunt = damage;
            hit.m_point = collision.contacts[0].point;
            hit.m_dir = rb != null ? rb.linearVelocity.normalized : transform.forward;
            hit.m_skill = Skills.SkillType.Clubs;
            hit.m_pushForce = LogItemThrower.LogPushForce.Value;
            hit.m_attacker = Player.m_localPlayer.GetZDOID();
            character.Damage(hit);

            if (_hitVfxPrefab != null)
                Instantiate(_hitVfxPrefab, collision.contacts[0].point, Quaternion.identity);

            ApplyAoe(collision.contacts[0].point, damage, character);
            Destroy(this);
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

                if (_hitVfxPrefab != null)
                    Instantiate(_hitVfxPrefab, point, Quaternion.identity);
            }
        }
    }
}