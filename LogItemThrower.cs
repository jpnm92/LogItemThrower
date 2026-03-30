using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SkillManager;
using System;
using System.Collections;
using UnityEngine;

namespace LogItemThrower
{
    [BepInPlugin("com.custom.logitemthrower", "Log/Item Thrower", "1.0.0")]
    [BepInDependency("WackyMole.EpicMMOSystem", BepInDependency.DependencyFlags.SoftDependency)]
    public class LogItemThrower : BaseUnityPlugin
    {
        // CONFIGURATION ENTRIES (Public Static)

        // Hotkeys
        public static ConfigEntry<KeyboardShortcut> LaunchHotkey;
        public static ConfigEntry<KeyboardShortcut> RotationHotkey;
        public static ConfigEntry<KeyboardShortcut> DropHotkey;

        // Stamina Costs (%)
        public static ConfigEntry<float> PctGrab, PctThrow, PctDrainStill, PctDrainMove;

        // Physics & Range
        public static ConfigEntry<float> LogForce, ItemForce, LogHoldRotationX, LogHoldRotationZ;
        public static ConfigEntry<float> LogPushForce, LogAoeRadius;
        public static ConfigEntry<float> GrabRange;

        // Damage & Modifiers
        public static ConfigEntry<float> StrengthCoefficient, SkillForceBonus;
        public static ConfigEntry<float> LogDamageCoefficient, LogAoeDamageMultiplier;


        // INTERNAL REFERENCES & REFLECTION (Private Static)
        private static SkillManager.Skill _throwingSkill;
        private static System.Reflection.MethodInfo _hideHandItemsMethod;
        private static System.Reflection.MethodInfo _showHandItemsMethod;


        // INSTANCE STATE VARIABLES (Private)

        // Player Interaction State
        private bool _isHolding;
        private bool _rotationMode = false;
        private bool _isHeldLog;

        // Held Object Data
        private GameObject _heldObject;
        private Rigidbody _heldRigidbody;
        private Quaternion _heldRotation = Quaternion.identity;

        // Cached Original Physics State
        private float _originalMass;
        private float _originalDamping;

        // UI & Media
        private bool _showingGrabHint = false;
        private GameObject _throwSoundPrefab;
        private readonly Harmony _harmony = new Harmony("com.custom.logitemthrower");

        void Awake()
        {
            _hideHandItemsMethod = typeof(Humanoid).GetMethod("HideHandItems",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            _showHandItemsMethod = typeof(Humanoid).GetMethod("ShowHandItems",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            SetupConfig();
            SetupSkill();
            Logger.LogInfo("Log/Item Thrower: Unity 6 Compatibility Mode Loaded.");
            _harmony.PatchAll();
        }
        
        private void SetupConfig()
        {
            LaunchHotkey = Config.Bind("General", "LaunchHotkey", new KeyboardShortcut(KeyCode.T), "Grab/Launch.");
            DropHotkey = Config.Bind("General", "DropHotkey", new KeyboardShortcut(KeyCode.G), "Drop held object.");
            LogForce = Config.Bind("Physics", "LogForce", 2000f, "Force for logs.");
            ItemForce = Config.Bind("Physics", "ItemForce", 150f, "Force for items.");
            StrengthCoefficient = Config.Bind("Physics", "StrengthCoefficient", 0.002f, "Force bonus per Strength point (e.g. 0.002 = +0.2% per point).");
            SkillForceBonus = Config.Bind("Physics", "SkillForceBonus", 0.25f, "Max force bonus from Throwing skill at level 100 (e.g. 0.25 = +25%).");
            LogDamageCoefficient = Config.Bind("Physics", "LogDamageCoefficient", 0.01f, "Damage dealt per unit of force for logs.");
            LogPushForce = Config.Bind("Physics", "LogPushForce", 80f, "Knockback force applied to enemies hit by a log.");
            GrabRange = Config.Bind("Physics", "GrabRange", 12.0f, "Reach distance.");
            RotationHotkey = Config.Bind("General", "RotationHotkey", new KeyboardShortcut(KeyCode.R), "Toggle rotation mode while holding.");
            PctGrab = Config.Bind("Stamina (%)", "GrabCostPct", 0.12f, "12% of Max Stamina.");
            PctThrow = Config.Bind("Stamina (%)", "ThrowCostPct", 0.15f, "15% of Max Stamina.");
            PctDrainStill = Config.Bind("Stamina (%)", "PassiveDrainPct", 0.005f, "0.5% per sec still.");
            PctDrainMove = Config.Bind("Stamina (%)", "ActiveDrainPct", 0.015f, "1.5% per sec moving.");
            LogHoldRotationX = Config.Bind("Physics", "LogHoldRotationX", 90f, "X rotation of held log in degrees.");
            LogHoldRotationZ = Config.Bind("Physics", "LogHoldRotationZ", 0f, "Z rotation of held log in degrees.");
            LogAoeRadius = Config.Bind("Physics", "LogAoeRadius", 3f, "Radius of AOE damage on log impact.");
            LogAoeDamageMultiplier = Config.Bind("Physics", "LogAoeDamageMultiplier", 0.5f, "AOE damage as a fraction of direct hit damage.");
        }
        //
        private void SetupSkill()
        {
            try
            {
                _throwingSkill = new SkillManager.Skill("Throwing", "throwing.png");
                _throwingSkill.Description.English("Improves log-throwing efficiency and power.");
                _throwingSkill.Configurable = true;
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"SkillManager icon failed: {ex.Message}");
                _throwingSkill = null;
            }
        }
        
        private float GetEfficiencyMultiplier()
        {
            float level = GetSkillLevel();
            float t = Mathf.Log(1f + level) / Mathf.Log(101f);
            return 1.0f - (t * 0.5f);
        }

        private float GetSkillLevel()
        {
            if (Player.m_localPlayer == null) return 0f;
            if (_throwingSkill == null) return 0f;
            return Player.m_localPlayer.GetSkillFactor("Throwing") * 100f;
        }

        void Update()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Chat.instance?.HasFocus() == true || Menu.IsVisible()) return;

            if (_isHolding)
            {
                if (_heldObject == null) { CancelHold(); return; }
                if (p.GetStamina() <= 0) { p.Message(MessageHud.MessageType.TopLeft, "Exhausted!"); CancelHold(); return; }

                HandleStaminaDrain(p);

                // Toggle rotation mode
                if (Input.GetKeyDown(RotationHotkey.Value.MainKey))
                {
                    _rotationMode = !_rotationMode;
                    p.Message(MessageHud.MessageType.TopLeft, _rotationMode ? "Rotation mode ON" : "Rotation mode OFF");
                }

                // In rotation mode, mouse movement rotates the held object
                if (_rotationMode)
                {
                    float mouseX = Input.GetAxis("Mouse X") * 5f;
                    float mouseY = Input.GetAxis("Mouse Y") * 5f;
                    _heldRotation = Quaternion.AngleAxis(-mouseX, Vector3.up) * 
                        Quaternion.AngleAxis(mouseY, Utils.GetMainCamera().transform.right) *
                        _heldRotation;
                }

                if (Input.GetKeyDown(DropHotkey.Value.MainKey)) { CancelHold(); return; }
            }

            // Show grab hint if looking at a valid object and not already holding something
            if (!_isHolding)
            {
                bool canGrab = false;
                GameObject hover = p.GetHoverObject();
                if (hover != null)
                {
                    Rigidbody rb = hover.GetComponentInParent<Rigidbody>();
                    bool isLog = hover.GetComponentInParent<TreeLog>() != null;
                    bool isItem = hover.GetComponentInParent<ItemDrop>() != null;
                    if (rb != null && (isLog || isItem) &&
                        Vector3.Distance(p.transform.position, rb.position) <= GrabRange.Value)
                        canGrab = true;
                }
                _showingGrabHint = canGrab;
            }
            else
            {
                _showingGrabHint = false;
            }
            if (Input.GetKeyDown(LaunchHotkey.Value.MainKey))
            {
                if (_isHolding) TryLaunch(p);
                else TryGrabHoverObject(p);
            }
        }

        private void HandleStaminaDrain(Player p)
        {
            if (!_isHeldLog) return;

            bool isMoving = p.GetVelocity().magnitude > 0.1f;
            float basePct = isMoving ? PctDrainMove.Value : PctDrainStill.Value;

            float drainPerSecond = p.GetMaxStamina() * basePct * GetEfficiencyMultiplier();
            p.UseStamina(drainPerSecond * Time.deltaTime);
        }

        private void TryGrabHoverObject(Player p)
        {
            GameObject hover = p.GetHoverObject();
            if (hover == null) return;

            Rigidbody rb = hover.GetComponentInParent<Rigidbody>();
            bool isLog = hover.GetComponentInParent<TreeLog>() != null;
            bool isItem = hover.GetComponentInParent<ItemDrop>() != null;

            if (rb != null && (isLog || isItem))
            {
                float pct = isLog ? PctGrab.Value : 0.02f;
                float cost = p.GetMaxStamina() * pct * GetEfficiencyMultiplier();

                if (p.HaveStamina(cost))
                {
                    if (Vector3.Distance(p.transform.position, rb.position) <= GrabRange.Value)
                    {
                        p.UseStamina(cost);
                        BeginHold(rb.gameObject, rb);
                    }
                }
                else { p.Message(MessageHud.MessageType.TopLeft, "Not enough energy!"); }
            }
        }

        private void BeginHold(GameObject obj, Rigidbody rb)
        {
            // If already holding something, drop it first
            _heldObject = obj;
            _isHeldLog = obj.GetComponentInParent<TreeLog>() != null;

            // If the object is an item, try to claim it to prevent desync
            _heldRigidbody = rb;
            _isHolding = true;

            // Set initial rotation to face the camera
            _heldRotation = Quaternion.identity;

            // Claim ownership if it's a networked object to prevent desync
            ZNetView znv = obj.GetComponent<ZNetView>();
            if (znv != null && znv.IsValid())
                znv.ClaimOwnership();

            // Hide player's hand items while holding
            _hideHandItemsMethod?.Invoke(Player.m_localPlayer, new object[] { false, false });
            _originalMass = _heldRigidbody.mass;
            _originalDamping = _heldRigidbody.linearDamping;

            // Make the held object kinematic and disable gravity while holding
            _heldRigidbody.mass = 0.1f;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;

            SetCollision(true);
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Target Secured.");
        }

        void FixedUpdate()
        {
            // Move held object to the hold position each physics tick
            if (_isHolding && _heldObject != null && _heldRigidbody != null && Player.m_localPlayer != null)
            {
                Transform pTrans = Player.m_localPlayer.transform;
                Vector3 holdPos = pTrans.position + (Vector3.up * 2.8f) + (pTrans.right * 1.3f) + (pTrans.forward * 0.4f);
                _heldRigidbody.MovePosition(holdPos);

                if (_isHeldLog && !_rotationMode)
                {
                    Vector3 flatForward = pTrans.forward;
                    flatForward.y = 0f;
                    flatForward.Normalize();
                    _heldRotation = Quaternion.LookRotation(flatForward) *
                        Quaternion.Euler(LogHoldRotationX.Value, 0f, LogHoldRotationZ.Value);
                }

                _heldRigidbody.MoveRotation(_heldRotation);
            }
        }

        private void TryLaunch(Player p)
        {
            bool isLog = _isHeldLog;
            float pct = isLog ? PctThrow.Value : 0.05f;
            float cost = p.GetMaxStamina() * pct * GetEfficiencyMultiplier();

            if (p.HaveStamina(cost))
            {
                p.UseStamina(cost);
                if (_throwingSkill != null)
                    p.RaiseSkill("Throwing", isLog ? 1.0f : 0.2f);
                Launch(isLog);
            }
            else { p.Message(MessageHud.MessageType.TopLeft, $"Too tired! Recover stamina or press [{DropHotkey.Value.MainKey}] to drop."); }
        }

        private void Launch(bool isLog)
        {
            float level = GetSkillLevel();
            float t = Mathf.Log(1f + level) / Mathf.Log(101f);
            float skillBonus = 1.0f + (t * SkillForceBonus.Value);

            int strength = 0;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("WackyMole.EpicMMOSystem"))
            {
                try
                {
                    var apiType = Type.GetType("API.EpicMMOSystem_API, EpicMMOSystem");
                    if (apiType != null)
                    {
                        var method = apiType.GetMethod("GetAttribute");
                        var attributType = apiType.GetNestedType("Attribut");
                        if (method != null && attributType != null)
                        {
                            var strengthValue = Enum.Parse(attributType, "Strength");
                            strength = (int)method.Invoke(null, new object[] { strengthValue });
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"EpicMMOSystem strength lookup failed: {ex.Message}");
                }
            }

            float strengthMultiplier = 1f + (strength * StrengthCoefficient.Value);
            float baseForce = isLog ? LogForce.Value : ItemForce.Value;
            float massScale = isLog ? 1f : Mathf.Clamp(_originalMass, 0.5f, 5f);
            float finalForce = baseForce * massScale * skillBonus * strengthMultiplier;

            if (isLog)
            {
                var proj = _heldObject.AddComponent<ThrownProjectile>();
                proj.damage = finalForce * LogDamageCoefficient.Value;
            }

            _heldObject.transform.position = Player.m_localPlayer.transform.position + (Player.m_localPlayer.transform.forward * 2.2f) + (Vector3.up * 1.8f);

            _heldRigidbody.isKinematic = false;
            _heldRigidbody.useGravity = true;
            _heldRigidbody.mass = _originalMass;
            _heldRigidbody.linearDamping = 0.2f;
            _heldRigidbody.WakeUp();

            Camera cam = Utils.GetMainCamera();
            Vector3 launchDir;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 200f))
                launchDir = (hit.point - _heldObject.transform.position).normalized;
            else
                launchDir = cam.transform.forward;

            if (_throwSoundPrefab == null)
                _throwSoundPrefab = ZNetScene.instance?.GetPrefab("sfx_warrior_attack");
            if (_throwSoundPrefab != null)
                Instantiate(_throwSoundPrefab, _heldObject.transform.position, Quaternion.identity);

            _heldRigidbody.AddForce(launchDir * finalForce, ForceMode.Impulse);

            if (!isLog) _heldObject.AddComponent<SkippingStone>();

            GameObject objToReset = _heldObject;
            if (Player.m_localPlayer != null)
                _showHandItemsMethod?.Invoke(Player.m_localPlayer, new object[] { false, true });
            ClearState();
            StartCoroutine(SafeCollisionReset(objToReset));
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, isLog ? "YEET!" : "Tossed.");
        }

        private IEnumerator SafeCollisionReset(GameObject obj)
        {
            yield return new WaitForSeconds(0.6f);
            if (obj != null)
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearDamping = _originalDamping;
                SetCollision(false, obj);
            }
        }

        private void SetCollision(bool ignore, GameObject target = null)
        {
            GameObject obj = target ?? _heldObject;
            if (obj == null || Player.m_localPlayer == null) return;
            Collider[] heldColliders = obj.GetComponentsInChildren<Collider>();
            Collider[] playerColliders = Player.m_localPlayer.GetComponentsInChildren<Collider>();
            foreach (var hc in heldColliders) foreach (var pc in playerColliders)
                Physics.IgnoreCollision(hc, pc, ignore);
        }

        private void ClearState() { _isHolding = false; _heldObject = null; _heldRigidbody = null; _heldRotation = Quaternion.identity; _rotationMode = false; _isHeldLog = false; }
        private void CancelHold()
        {
            if (_heldRigidbody != null)
            {
                _heldRigidbody.isKinematic = false;
                _heldRigidbody.useGravity = true;
                _heldRigidbody.mass = _originalMass;
                _heldRigidbody.linearDamping = _originalDamping;
                SetCollision(false);
            }
            if (Player.m_localPlayer != null)
                _showHandItemsMethod?.Invoke(Player.m_localPlayer, new object[] { false, true });
            ClearState();
        }
    }
}