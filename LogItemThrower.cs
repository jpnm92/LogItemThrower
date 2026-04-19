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
    [BepInPlugin("com.custom.logitemthrower", "Log/Item Thrower", "1.0.1")]
    [BepInDependency("WackyMole.EpicMMOSystem", BepInDependency.DependencyFlags.SoftDependency)]
    public class LogItemThrower : BaseUnityPlugin
    {
        public static ConfigEntry<KeyboardShortcut> LaunchHotkey;
        public static ConfigEntry<KeyboardShortcut> RotationHotkey;
        public static ConfigEntry<KeyboardShortcut> DropHotkey;

        public static ConfigEntry<float> PctGrab, PctThrow, PctDrainStill, PctDrainMove;
        public static ConfigEntry<float> PctGrabItem, PctThrowItem;

        public static ConfigEntry<float> LogForce, ItemForce, LogHoldRotationX, LogHoldRotationZ;
        public static ConfigEntry<float> LogPushForce, LogAoeRadius;
        public static ConfigEntry<float> GrabRange;

        public static ConfigEntry<float> StrengthCoefficient, SkillForceBonus;
        public static ConfigEntry<float> LogDamageCoefficient, LogAoeDamageMultiplier;

        public static ConfigEntry<float> MaxChargeTime;
        public static ConfigEntry<float> MaxChargeMultiplier;
        public static ConfigEntry<float> MaxChargeStaminaMultiplier;

        private static SkillManager.Skill _throwingSkill;
        private static System.Reflection.MethodInfo _hideHandItemsMethod;
        private static System.Reflection.MethodInfo _showHandItemsMethod;

        private static bool _epicMmoReflectionInitialized;
        private static System.Reflection.MethodInfo _epicGetAttributeMethod;
        private static object _epicStrengthValue;
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource("LogItemThrower");

        private bool _isHolding;
        private bool _rotationMode = false;
        private bool _isHeldLog;
        private bool _isCharging;
        private bool _justGrabbed = false;
        private float _chargeTime;

        private GameObject _heldObject;
        private Rigidbody _heldRigidbody;
        private Quaternion _heldRotation = Quaternion.identity;
        private Vector3 _originalScale;

        private float _originalMass;
        private float _originalDamping;

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
            LaunchHotkey = Config.Bind("1 - General", "LaunchHotkey", new KeyboardShortcut(KeyCode.T),
                new ConfigDescription("Key to grab and throw logs/items.", null,
                new ConfigurationManagerAttributes { Order = 10 }));

            DropHotkey = Config.Bind("1 - General", "DropHotkey", new KeyboardShortcut(KeyCode.G),
                new ConfigDescription("Key to drop the held object.", null,
                new ConfigurationManagerAttributes { Order = 9 }));

            RotationHotkey = Config.Bind("1 - General", "RotationHotkey", new KeyboardShortcut(KeyCode.R),
                new ConfigDescription("Key to toggle rotation mode while holding.", null,
                new ConfigurationManagerAttributes { Order = 8 }));

            GrabRange = Config.Bind("2 - Physics", "GrabRange", 12.0f,
                new ConfigDescription("Max reach distance to grab an object.", new AcceptableValueRange<float>(1f, 30f),
                new ConfigurationManagerAttributes { Order = 10 }));

            LogForce = Config.Bind("2 - Physics", "LogForce", 2000f,
                new ConfigDescription("Launch force applied to logs.", new AcceptableValueRange<float>(100f, 10000f),
                new ConfigurationManagerAttributes { Order = 9 }));

            ItemForce = Config.Bind("2 - Physics", "ItemForce", 150f,
                new ConfigDescription("Launch force applied to items.", new AcceptableValueRange<float>(10f, 2000f),
                new ConfigurationManagerAttributes { Order = 8 }));

            LogPushForce = Config.Bind("2 - Physics", "LogPushForce", 80f,
                new ConfigDescription("Knockback force applied to enemies hit by a log.", new AcceptableValueRange<float>(0f, 500f),
                new ConfigurationManagerAttributes { Order = 7 }));

            LogDamageCoefficient = Config.Bind("2 - Physics", "LogDamageCoefficient", 0.01f,
                new ConfigDescription("Damage dealt per unit of launch force.", new AcceptableValueRange<float>(0f, 0.1f),
                new ConfigurationManagerAttributes { Order = 6 }));

            StrengthCoefficient = Config.Bind("2 - Physics", "StrengthCoefficient", 0.002f,
                new ConfigDescription("Force bonus multiplier per Strength point (EpicMMO).", new AcceptableValueRange<float>(0f, 0.05f),
                new ConfigurationManagerAttributes { Order = 5 }));

            SkillForceBonus = Config.Bind("2 - Physics", "SkillForceBonus", 0.25f,
                new ConfigDescription("Max force bonus from max Throwing skill level.", new AcceptableValueRange<float>(0f, 2f),
                new ConfigurationManagerAttributes { Order = 4 }));

            LogHoldRotationX = Config.Bind("2 - Physics", "LogHoldRotationX", 90f,
                new ConfigDescription("X-axis rotation of the log while held on shoulder.", new AcceptableValueRange<float>(-180f, 180f),
                new ConfigurationManagerAttributes { Order = 3 }));

            LogHoldRotationZ = Config.Bind("2 - Physics", "LogHoldRotationZ", 0f,
                new ConfigDescription("Z-axis rotation of the log while held on shoulder.", new AcceptableValueRange<float>(-180f, 180f),
                new ConfigurationManagerAttributes { Order = 2 }));

            LogAoeRadius = Config.Bind("2 - Physics", "LogAoeRadius", 3f,
                new ConfigDescription("Radius of AOE splash damage on log impact.", new AcceptableValueRange<float>(0f, 15f),
                new ConfigurationManagerAttributes { Order = 1 }));

            LogAoeDamageMultiplier = Config.Bind("2 - Physics", "LogAoeDamageMultiplier", 0.5f,
                new ConfigDescription("AOE damage as a fraction of direct hit damage.", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 0 }));

            PctGrab = Config.Bind("3 - Stamina", "GrabCostPct", 0.12f,
                new ConfigDescription("Stamina cost to grab a log (% of max stamina).", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 4 }));

            PctGrabItem = Config.Bind("3 - Stamina", "ItemGrabCostPct", 0.02f,
                new ConfigDescription("Stamina cost to grab an item stack (% of max stamina).", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 5 }));

            PctThrow = Config.Bind("3 - Stamina", "ThrowCostPct", 0.15f,
                new ConfigDescription("Stamina cost to throw a log (% of max stamina).", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 3 }));

            PctThrowItem = Config.Bind("3 - Stamina", "ItemThrowCostPct", 0.05f,
                new ConfigDescription("Stamina cost to throw an item stack (% of max stamina).", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 3 }));

            PctDrainStill = Config.Bind("3 - Stamina", "PassiveDrainPct", 0.005f,
                new ConfigDescription("Stamina drained per second while holding still (% of max).", new AcceptableValueRange<float>(0f, 0.1f),
                new ConfigurationManagerAttributes { Order = 2 }));

            PctDrainMove = Config.Bind("3 - Stamina", "ActiveDrainPct", 0.015f,
                new ConfigDescription("Stamina drained per second while moving with a log (% of max).", new AcceptableValueRange<float>(0f, 0.1f),
                new ConfigurationManagerAttributes { Order = 1 }));

            MaxChargeTime = Config.Bind("4 - Charge", "MaxChargeTime", 2.0f,
                new ConfigDescription("Seconds to hold throw key for maximum charge.", new AcceptableValueRange<float>(0.1f, 10f),
                new ConfigurationManagerAttributes { Order = 3 }));

            MaxChargeMultiplier = Config.Bind("4 - Charge", "MaxChargeMultiplier", 2.0f,
                new ConfigDescription("Force multiplier at full charge.", new AcceptableValueRange<float>(1f, 5f),
                new ConfigurationManagerAttributes { Order = 2 }));

            MaxChargeStaminaMultiplier = Config.Bind("4 - Charge", "MaxChargeStaminaMultiplier", 1.5f,
                new ConfigDescription("Stamina cost multiplier at full charge.", new AcceptableValueRange<float>(1f, 3f),
                new ConfigurationManagerAttributes { Order = 1 }));
        }

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

        private static int GetEpicStrength()
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("WackyMole.EpicMMOSystem"))
                return 0;

            if (!_epicMmoReflectionInitialized)
            {
                _epicMmoReflectionInitialized = true;
                try
                {
                    var apiType = Type.GetType("API.EpicMMOSystem_API, EpicMMOSystem");
                    if (apiType != null)
                    {
                        var method = apiType.GetMethod("GetAttribute");
                        var attributType = apiType.GetNestedType("Attribut");
                        if (method != null && attributType != null)
                        {
                            _epicGetAttributeMethod = method;
                            _epicStrengthValue = Enum.Parse(attributType, "Strength");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _staticLog.LogWarning($"EpicMMOSystem reflection setup failed: {ex.Message}");
                }
            }

            if (_epicGetAttributeMethod == null || _epicStrengthValue == null) return 0;

            try
            {
                object result = _epicGetAttributeMethod.Invoke(null, new object[] { _epicStrengthValue });
                if (result == null) return 0;
                if (result is int intValue) return intValue;
                if (result is float floatValue) return Mathf.RoundToInt(floatValue);
                return Convert.ToInt32(result);
            }
            catch (System.Exception ex)
            {
                _staticLog.LogWarning($"EpicMMOSystem strength lookup failed: {ex.Message}");
                return 0;
            }
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

                if (Input.GetKeyDown(RotationHotkey.Value.MainKey))
                {
                    _rotationMode = !_rotationMode;
                    p.Message(MessageHud.MessageType.TopLeft, _rotationMode ? "Rotation mode ON" : "Rotation mode OFF");
                }

                if (_rotationMode)
                {
                    float mouseX = Input.GetAxis("Mouse X") * 5f;
                    float mouseY = Input.GetAxis("Mouse Y") * 5f;
                    _heldRotation = Quaternion.AngleAxis(-mouseX, Vector3.up) * Quaternion.AngleAxis(mouseY, Utils.GetMainCamera().transform.right) *
                        _heldRotation;
                }

                if (Input.GetKeyDown(DropHotkey.Value.MainKey)) { CancelHold(); return; }
            }

            if (Input.GetKeyDown(LaunchHotkey.Value.MainKey))
            {
                if (!_isHolding)
                {
                    TryGrabHoverObject(p);
                    _justGrabbed = _isHolding;
                }
            }

            if (_isHolding && Input.GetKey(LaunchHotkey.Value.MainKey) && !_justGrabbed)
            {
                _isCharging = true;
                _chargeTime = Mathf.Min(_chargeTime + Time.deltaTime, MaxChargeTime.Value);
            }

            if (_isHolding && Input.GetKeyUp(LaunchHotkey.Value.MainKey))
            {
                if (_justGrabbed)
                {
                    _justGrabbed = false;
                    _isCharging = false;
                    _chargeTime = 0f;
                }
                else if (_isCharging)
                {
                    TryLaunch(p);
                    _isCharging = false;
                    _chargeTime = 0f;
                }
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
                float pct = isLog ? PctGrab.Value : PctGrabItem.Value;
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
            _heldObject = obj;
            _isHeldLog = obj.GetComponentInParent<TreeLog>() != null;

            _originalScale = obj.transform.localScale;
            if (_isHeldLog)
            {
                obj.transform.localScale = _originalScale * 0.35f;
            }

            _heldRigidbody = rb;
            _isHolding = true;
            _heldRotation = Quaternion.identity;

            ZNetView znv = obj.GetComponent<ZNetView>();
            if (znv != null && znv.IsValid())
                znv.ClaimOwnership();

            _hideHandItemsMethod?.Invoke(Player.m_localPlayer, new object[] { false, false });
            _originalMass = _heldRigidbody.mass;
            _originalDamping = _heldRigidbody.linearDamping;

            _heldRigidbody.mass = 0.1f;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;

            foreach (var col in _heldObject.GetComponentsInChildren<Collider>())
                col.enabled = false;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Target Secured.");
        }

        void FixedUpdate()
        {
            if (_isHolding && _heldObject != null && _heldRigidbody != null && Player.m_localPlayer != null)
            {
                Transform pTrans = Player.m_localPlayer.transform;
                Vector3 holdPos = pTrans.position + (Vector3.up * 1.8f) + (pTrans.right * 0.4f) + (pTrans.forward * -0.2f);
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
            float chargeRatio = MaxChargeTime.Value > 0f ? _chargeTime / MaxChargeTime.Value : 1f;
            bool isLog = _isHeldLog;
            float pct = isLog ? PctThrow.Value : PctThrowItem.Value;
            float chargeStaminaMult = 1f + (chargeRatio * (MaxChargeStaminaMultiplier.Value - 1f));
            float cost = p.GetMaxStamina() * pct * GetEfficiencyMultiplier() * chargeStaminaMult;

            if (p.HaveStamina(cost))
            {
                p.UseStamina(cost);
                if (_throwingSkill != null)
                    p.RaiseSkill("Throwing", isLog ? 1.0f : 0.2f);
                Launch(isLog, chargeRatio);
            }
            else { p.Message(MessageHud.MessageType.TopLeft, $"Too tired! Recover stamina or press [{DropHotkey.Value.MainKey}] to drop."); }
        }

        private void Launch(bool isLog, float chargeRatio = 1f)
        {
            float level = GetSkillLevel();
            float t = Mathf.Log(1f + level) / Mathf.Log(101f);
            float skillBonus = 1.0f + (t * SkillForceBonus.Value);

            int strength = GetEpicStrength();

            float strengthMultiplier = 1f + (strength * StrengthCoefficient.Value);
            float baseForce = isLog ? LogForce.Value : ItemForce.Value;
            float massScale = isLog ? 1f : Mathf.Clamp(_originalMass, 0.5f, 5f);
            float chargeForceMult = 1f + (chargeRatio * (MaxChargeMultiplier.Value - 1f));
            float finalForce = baseForce * massScale * skillBonus * strengthMultiplier * chargeForceMult;

            if (isLog)
            {
                var proj = _heldObject.AddComponent<ThrownProjectile>();
                proj.damage = finalForce * LogDamageCoefficient.Value;
            }

            _heldObject.transform.position = Player.m_localPlayer.transform.position + (Player.m_localPlayer.transform.forward * 2.2f) + (Vector3.up * 1.8f);
            _heldObject.transform.localScale = _originalScale;
            _heldRigidbody.mass = _originalMass;
            _heldRigidbody.linearDamping = 0.2f;
            _heldRigidbody.WakeUp();

            Camera cam = Utils.GetMainCamera();
            Vector3 launchDir = cam.transform.forward;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 200f))
            {
                Vector3 toHit = (hit.point - _heldObject.transform.position).normalized;
                if (Vector3.Dot(toHit, cam.transform.forward) > 0.1f)
                    launchDir = toHit;
            }

            if (_throwSoundPrefab == null)
                _throwSoundPrefab = ZNetScene.instance?.GetPrefab("sfx_warrior_attack");
            if (_throwSoundPrefab != null)
                Instantiate(_throwSoundPrefab, _heldObject.transform.position, Quaternion.identity);

            if (!isLog) _heldObject.AddComponent<SkippingStone>();

            GameObject objToReset = _heldObject;
            Rigidbody rbToLaunch = _heldRigidbody;
            float dampingToRestore = _originalDamping;
            if (Player.m_localPlayer != null)
                _showHandItemsMethod?.Invoke(Player.m_localPlayer, new object[] { false, true });
            ClearState();
            StartCoroutine(DelayedLaunch(objToReset, rbToLaunch, launchDir, finalForce, dampingToRestore));
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, isLog ? "YEET!" : "Tossed.");
        }

        void OnGUI()
        {
            if (!_isCharging || !_isHolding) return;

            float ratio = MaxChargeTime.Value > 0f ? _chargeTime / MaxChargeTime.Value : 1f;
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            float radius = 30f;
            float size = 8f;

            Color color = Color.Lerp(Color.yellow, Color.red, ratio);
            GUI.color = color;

            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f * ratio;
                float x = cx + Mathf.Sin(angle) * radius - size / 2f;
                float y = cy - Mathf.Cos(angle) * radius - size / 2f;
                GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
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

        private void ClearState()
        {
            _isHolding = false; _heldObject = null; _heldRigidbody = null;
            _heldRotation = Quaternion.identity; _rotationMode = false; _isHeldLog = false;
            _isCharging = false; _chargeTime = 0f;
            _justGrabbed = false;
        }

        private void CancelHold()
        {
            if (_heldObject == null)
            {
                ClearState();
                return;
            }

            foreach (var col in _heldObject.GetComponentsInChildren<Collider>())
                col.enabled = true;

            if (_heldRigidbody != null)
            {
                _heldObject.transform.localScale = _originalScale;
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
        private IEnumerator DelayedLaunch(GameObject obj, Rigidbody rb, Vector3 dir, float force, float originalDamping)
        {
            yield return new WaitForFixedUpdate();

            if (obj == null || rb == null) yield break;

            foreach (var col in obj.GetComponentsInChildren<Collider>())
                col.enabled = true;

            SetCollision(true, obj);

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(dir * force, ForceMode.Impulse);

            yield return new WaitForSeconds(0.6f);

            if (rb != null) rb.linearDamping = originalDamping;
            if (obj != null) SetCollision(false, obj);
        }
    }
}