using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace LogLauncher
{
    [BepInPlugin("com.custom.loglauncher", "Log Launcher", "1.0.0")]
    public class LogItemThrower : BaseUnityPlugin
    {
        public static ConfigEntry<KeyboardShortcut> LaunchHotkey;
        public static ConfigEntry<float> LaunchForce;
        public static ConfigEntry<float> LaunchRange;
        public static ConfigEntry<float> UpwardAngle;
        public static ConfigEntry<bool> EnableLogs;
        public static ConfigEntry<bool> EnableItemStacks;

        internal static ManualLogSource StaticLogger;

        // State machine
        private GameObject _heldObject;
        private Rigidbody _heldRigidbody;
        private bool _isHolding;

        void Awake()
        {
            StaticLogger = Logger;

            LaunchHotkey = Config.Bind("General", "LaunchHotkey", new KeyboardShortcut(KeyCode.T),
                "First press picks up a log or item stack. Second press launches it.");

            LaunchForce = Config.Bind("General", "LaunchForce", 800f,
                "How hard the object gets launched.");

            LaunchRange = Config.Bind("General", "LaunchRange", 10f,
                "Maximum distance in meters to grab a log or item stack.");

            UpwardAngle = Config.Bind("General", "UpwardAngle", 15f,
                "Degrees upward added to the launch angle. Helps objects clear the ground.");

            EnableLogs = Config.Bind("General", "EnableLogs", true,
                "If true, tree logs can be grabbed and launched.");

            EnableItemStacks = Config.Bind("General", "EnableItemStacks", true,
                "If true, item stacks on the ground can be grabbed and launched.");

            Logger.LogInfo("Log Launcher: Locked and loaded.");
        }

        void Update()
        {
            if (Player.m_localPlayer == null) return;

            // Cancel hold if any UI opens
            if (Chat.instance?.HasFocus() == true ||
                global::Console.IsVisible() ||
                Menu.IsVisible() ||
                TextViewer.instance?.IsVisible() == true ||
                TextInput.IsVisible() ||
                StoreGui.IsVisible() ||
                InventoryGui.IsVisible())
            {
                if (_isHolding) CancelHold();
                return;
            }

            // If holding, move object to float over the player's shoulder each frame
            if (_isHolding)
            {
                if (_heldObject == null)
                {
                    CancelHold();
                    return;
                }

                Vector3 holdPos = Player.m_localPlayer.transform.position
                    + Vector3.up * 2.5f
                    + Player.m_localPlayer.transform.forward * -0.5f
                    + Player.m_localPlayer.transform.right * 0.5f;

                _heldObject.transform.position = holdPos;

                // Escape cancels the hold
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelHold();
                    return;
                }
            }

            if (!Input.GetKeyDown(LaunchHotkey.Value.MainKey)) return;

            foreach (var mod in LaunchHotkey.Value.Modifiers)
                if (!Input.GetKey(mod)) return;

            if (_isHolding)
                Launch();
            else
                TryGrab();
        }

        private void TryGrab()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, LaunchRange.Value))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Nothing to grab.");
                return;
            }

            // Try TreeLog first
            if (EnableLogs.Value)
            {
                TreeLog log = hit.collider.GetComponentInParent<TreeLog>();
                if (log != null)
                {
                    Rigidbody rb = log.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        BeginHold(log.gameObject, rb);
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Log ready — press T to launch!");
                        return;
                    }
                }
            }

            // Try ItemDrop
            if (EnableItemStacks.Value)
            {
                ItemDrop itemDrop = hit.collider.GetComponentInParent<ItemDrop>();
                if (itemDrop != null)
                {
                    Rigidbody rb = itemDrop.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        string itemName = Localization.instance.Localize(itemDrop.m_itemData.m_shared.m_name);
                        BeginHold(itemDrop.gameObject, rb);
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{itemName} ready — press T to launch!");
                        return;
                    }
                }
            }

            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Nothing to grab.");
        }

        private void BeginHold(GameObject obj, Rigidbody rb)
        {
            _heldObject = obj;
            _heldRigidbody = rb;
            _isHolding = true;
            _heldRigidbody.isKinematic = true;
        }

        private void Launch()
        {
            if (_heldObject == null || _heldRigidbody == null)
            {
                CancelHold();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) { CancelHold(); return; }

            // Re-enable physics before applying force
            _heldRigidbody.isKinematic = false;
            _heldRigidbody.WakeUp();

            Vector3 launchDir = Quaternion.AngleAxis(-UpwardAngle.Value, cam.transform.right) * cam.transform.forward;

            _heldRigidbody.AddForce(launchDir * LaunchForce.Value, ForceMode.Impulse);

            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Timber!");
            StaticLogger.LogInfo($"Launched {_heldObject.name}.");

            _heldObject = null;
            _heldRigidbody = null;
            _isHolding = false;
        }

        private void CancelHold()
        {
            if (_heldRigidbody != null)
            {
                _heldRigidbody.isKinematic = false;
                _heldRigidbody.WakeUp();
            }

            _heldObject = null;
            _heldRigidbody = null;
            _isHolding = false;

            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Cancelled.");
        }
    }
}