using UnityEngine;

namespace LogItemThrower
{
    public class SkippingStone : MonoBehaviour
    {
        private Rigidbody rb;
        private int skipsRemaining = 5;
        private float lastSkipTime;
        private WaterVolume _waterVol = null;
        private int _waterLayer;
        private GameObject _splashPrefab;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            _waterLayer = LayerMask.NameToLayer("Water");
            _splashPrefab = ZNetScene.instance?.GetPrefab("vfx_water_hit");
            Destroy(this, 10f);
        }

        void FixedUpdate()
        {
            if (rb == null || skipsRemaining <= 0) return;
            float waterLevel = Floating.GetWaterLevel(transform.position, ref _waterVol);
            if (transform.position.y <= waterLevel && Time.time - lastSkipTime > 0.2f)
            {
                if (rb.linearVelocity.magnitude > 5f)
                {
                    Vector3 currentVel = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(currentVel.x, Mathf.Abs(currentVel.y) * 0.8f + 5f, currentVel.z);
                    rb.AddForce(transform.forward * 2f, ForceMode.VelocityChange);

                    skipsRemaining--;
                    lastSkipTime = Time.time;


                    if (_splashPrefab != null)
                        Instantiate(_splashPrefab, new Vector3(transform.position.x, waterLevel, transform.position.z), Quaternion.identity);

                }
                else { Destroy(this); }
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            // Only destroy the skipping behaviour on non-water collision
            // if we've already skipped at least once — otherwise land throws
            // would instantly kill the component before it does anything
            if (skipsRemaining < 5 && collision.gameObject.layer != _waterLayer)
                Destroy(this);
        }
    }
}