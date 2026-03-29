
using UnityEngine;

namespace LogItemThrower
{
    public class ThrownProjectile : MonoBehaviour
    {
        public float damage;
        public bool isLog;
        private Rigidbody rb;
        private bool _hasHit = false;
        private GameObject _hitVfxPrefab;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            Destroy(this, 10f);
            _hitVfxPrefab = ZNetScene.instance?.GetPrefab("vfx_clubhit");
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
            hit.m_pushForce = isLog ? LogItemThrower.LogPushForce.Value : LogItemThrower.ItemPushForce.Value;
            hit.m_attacker = Player.m_localPlayer.GetZDOID();
            character.Damage(hit);
            // VFX
            if (_hitVfxPrefab != null)
                Instantiate(_hitVfxPrefab, collision.contacts[0].point, Quaternion.identity);
            Destroy(this);
        }
    }
}
