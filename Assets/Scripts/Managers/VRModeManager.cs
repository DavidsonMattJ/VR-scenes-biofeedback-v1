using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;

/// <summary>
/// Switches the whole experiment between desktop (debug) and VIVE Focus (VR) with one tickbox.
///
/// Put ONE of these in the Start scene (e.g. on an empty "VRMode" GameObject). It persists across
/// scenes. When "Play In VR" is ticked it:
///   1. starts OpenXR (so "Initialize XR on Startup" must be UNticked in XR Plug-in Management),
///   2. spawns one persistent XR rig and makes its camera the main camera,
///   3. in every scene: disables that scene's desktop camera(s) and puts the headset where the
///      desktop camera was (position + facing), so the orb sits in the same place in view,
///   4. turns screen-space canvases (questionnaires, Start/End UI) into world-space panels in
///      front of the participant and lets the VIVE controllers point-and-click them.
/// When unticked, nothing is changed: scenes run exactly as before with the desktop camera.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class VRModeManager : MonoBehaviour
{
    public static VRModeManager Instance { get; private set; }

    /// <summary>True once the headset is running and the rig is active. Other scripts can check this.</summary>
    public static bool IsVR => Instance != null && Instance.vrActive;

    /// <summary>The camera currently showing the participant's view (headset camera in VR).</summary>
    public static Camera ActiveCamera => IsVR ? Instance.vrCamera : Camera.main;

    [Header("Mode")]
    [Tooltip("Ticked: render to the VIVE Focus via VIVE Streaming.\nUnticked: normal desktop camera, for debugging.")]
    public bool playInVR = false;

    [Header("VR rig")]
    [Tooltip("An XR Origin prefab with controllers, e.g. 'XR Origin (XR Rig)' from the XR Interaction Toolkit Starter Assets sample.")]
    public GameObject vrRigPrefab;

    [Tooltip("Turn off joystick move/turn/teleport on the rig so the participant stays put.")]
    public bool disableLocomotion = true;

    [Tooltip("Hide the controllers and their pointer lines in scenes with no buttons or sliders (the environments), so they don't distract from the orb.")]
    public bool hideControllersWithoutUI = true;

    [Tooltip("Seconds to wait for the headset to report tracking before placing the view.")]
    public float trackingWaitSeconds = 3f;

    [Header("Questionnaire / UI panels in VR")]
    [Tooltip("Distance of UI panels in front of the participant (m).")]
    public float uiDistance = 1.6f;

    [Tooltip("Width of a full-screen canvas once it becomes a panel (m).")]
    public float uiWidthMetres = 1.6f;

    [Tooltip("Vertical offset of UI panels relative to eye height (m). Slightly below eye level is comfortable.")]
    public float uiHeightOffset = -0.15f;

    [Header("Operator")]
    [Tooltip("Key the RA presses (with the Game view focused) to re-centre the participant's view.")]
    public KeyCode recenterKey = KeyCode.R;

    // runtime state
    bool vrActive;
    bool weStartedXR;
    bool starting;
    GameObject rig;
    XROrigin xrOrigin;
    Camera vrCamera;
    Coroutine setupRoutine;
    readonly List<GameObject> controllerObjects = new List<GameObject>();
    float nextUiCheck;
    bool warnedNoTracking;
    bool warnedMaintainError;
    readonly HashSet<int> switchedOffCameras = new HashSet<int>();
    readonly HashSet<int> disabledEventSystems = new HashSet<int>();
    EventSystem vrEventSystem;
    readonly HashSet<int> convertedCanvases = new HashSet<int>();
    readonly HashSet<int> warnedResetCanvases = new HashSet<int>();

    // the pose the participant should be at in the current scene (from that scene's desktop camera)
    bool haveAnchor;
    Vector3 anchorPosition;
    float anchorYaw;

    // ---------------------------------------------------------------- lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Start scene was reloaded: keep the original manager.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;

        if (!playInVR)
        {
            // Safety: if XR was set to start automatically, it would grab the headset even in debug mode.
            if (manager != null && manager.activeLoader != null)
            {
                Debug.LogWarning("[VR] XR was started automatically although Play In VR is unticked. " +
                                 "Untick 'Initialize XR on Startup' in Project Settings > XR Plug-in Management. Stopping XR.");
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            Debug.Log("[VR] Mode: DESKTOP (Play In VR unticked).");
            return;
        }

        StartCoroutine(StartVR());
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopVR();
        Instance = null;
    }

    void OnApplicationQuit() => StopVR();

    void Update()
    {
        if (!vrActive) return;

        if (RecenterPressed()) Recenter();

        // Keep catching UI that appears after the scene loaded (panels switched on later, canvases
        // created by scripts, or UI carried over from another scene), not just what existed at load.
        if (setupRoutine == null && Time.unscaledTime >= nextUiCheck)
        {
            nextUiCheck = Time.unscaledTime + 0.25f;
            SafeMaintainUI();
        }
    }

    void SafeMaintainUI()
    {
        try
        {
            DisableStrayCameras();
            MaintainUI();
        }
        catch (System.Exception e)
        {
            if (!warnedMaintainError)
            {
                warnedMaintainError = true;
                Debug.LogError($"[VR] UI check failed (reported once): {e}");
            }
        }
    }

    /// <summary>
    /// Any other camera that is switched on (created or enabled after scene load, or in a scene that
    /// wasn't set up) would draw a head-locked view over the headset image, so switch it off.
    /// </summary>
    void DisableStrayCameras()
    {
#if UNITY_2022_2_OR_NEWER
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        Camera[] cams = FindObjectsOfType<Camera>();
#endif
        foreach (var cam in cams)
        {
            if (cam == vrCamera || !cam.enabled || cam.targetTexture != null) continue;
            if (rig != null && cam.transform.IsChildOf(rig.transform)) continue;

            cam.enabled = false;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            if (switchedOffCameras.Add(cam.GetInstanceID()))
                Debug.Log($"[VR] Switched off desktop camera '{cam.name}' (scene '{cam.gameObject.scene.name}') so it doesn't cover the headset view.");
        }
    }

    /// <summary>One block per scene listing its canvases, cameras and EventSystems, for debugging.</summary>
    void LogUIReport(Scene scene)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[VR] Scene '{scene.name}' set up. UI report:");

#if UNITY_2022_2_OR_NEWER
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        Camera[] cams = Resources.FindObjectsOfTypeAll<Camera>();
        EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>();
#endif
        foreach (var c in canvases)
        {
            if (!c.gameObject.scene.IsValid()) continue; // skip prefab assets
            if (c.transform.parent != null && c.transform.parent.GetComponentInParent<Canvas>(true) != null) continue;
            string parent = c.transform.parent != null ? c.transform.parent.name : "-";
            sb.Append($"\n  Canvas '{c.name}' [{c.gameObject.scene.name}] mode={c.renderMode} active={c.gameObject.activeInHierarchy} parent={parent}" +
                      $" xrRaycaster={(c.GetComponent<TrackedDeviceGraphicRaycaster>() != null)} keepOnDesktop={(c.GetComponent<VRKeepOnDesktop>() != null)}");
        }
        foreach (var cam in cams)
        {
            if (!cam.gameObject.scene.IsValid()) continue;
            sb.Append($"\n  Camera '{cam.name}' [{cam.gameObject.scene.name}] enabled={cam.enabled} active={cam.gameObject.activeInHierarchy} isVR={(cam == vrCamera)}");
        }
        foreach (var es in systems)
        {
            if (!es.gameObject.scene.IsValid()) continue;
            var modules = new List<string>();
            foreach (var m in es.GetComponents<BaseInputModule>()) modules.Add($"{m.GetType().Name}{(m.enabled ? "" : "(off)")}");
            sb.Append($"\n  EventSystem '{es.name}' [{es.gameObject.scene.name}] active={es.gameObject.activeInHierarchy} current={(EventSystem.current == es)} modules={string.Join(",", modules)}");
        }
        Debug.Log(sb.ToString());
    }

    // ---------------------------------------------------------------- XR start / stop

    IEnumerator StartVR()
    {
        starting = true;
        var settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null)
        {
            Debug.LogError("[VR] XR Plug-in Management is not set up. Install it and enable OpenXR for the Windows platform. Running on DESKTOP.");
            starting = false;
            yield break;
        }

        var manager = settings.Manager;
        if (manager.activeLoader == null)
        {
            Debug.Log("[VR] Starting OpenXR…");
            yield return manager.InitializeLoader();
            weStartedXR = manager.activeLoader != null;
        }

        if (manager.activeLoader == null)
        {
            Debug.LogError("[VR] Could not start the headset. Check that VIVE Hub shows the Focus as connected " +
                           "(VIVE Streaming running in the headset) and that the OpenXR runtime is set. Running on DESKTOP.");
            starting = false;
            yield break;
        }

        manager.StartSubsystems();

        if (!CreateRig())
        {
            StopVR();
            starting = false;
            yield break;
        }

        vrActive = true;
        starting = false;
        Debug.Log($"[VR] Mode: VR ({manager.activeLoader.name}). Press {recenterKey} in the Game view to re-centre.");

        // The first scene finished loading while XR was starting, so set it up now.
        RunSceneSetup(SceneManager.GetActiveScene());
    }

    void StopVR()
    {
        vrActive = false;
        if (!weStartedXR) return;
        weStartedXR = false;

        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager != null && manager.activeLoader != null)
        {
            manager.StopSubsystems();
            manager.DeinitializeLoader();
            Debug.Log("[VR] Headset released.");
        }
    }

    bool CreateRig()
    {
        if (vrRigPrefab == null)
        {
            Debug.LogError("[VR] 'Vr Rig Prefab' is empty on VRModeManager. Assign the XR Origin (XR Rig) prefab. Running on DESKTOP.");
            return false;
        }

        // Build the rig under an inactive holder so none of its interactors register with anything
        // until it has a persistent Interaction Manager to register with.
        var holder = new GameObject("VR Rig holder");
        holder.SetActive(false);
        rig = Instantiate(vrRigPrefab, holder.transform);
        rig.name = "VR Rig (persistent)";

        xrOrigin = rig.GetComponentInChildren<XROrigin>(true);
        vrCamera = xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera : rig.GetComponentInChildren<Camera>(true);

        if (vrCamera == null)
        {
            Debug.LogError("[VR] The rig prefab has no Camera. Use an XR Origin prefab. Running on DESKTOP.");
            Destroy(holder);
            rig = null;
            return false;
        }

        // XRI otherwise auto-creates the manager inside the current scene, where it is destroyed on the next
        // scene load ("Cannot register Near-Far Interactor … before its containing Interaction Group").
        if (rig.GetComponentInChildren<XRInteractionManager>(true) == null)
        {
            var managerGo = new GameObject("XR Interaction Manager (persistent)");
            DontDestroyOnLoad(managerGo);
            managerGo.AddComponent<XRInteractionManager>();
        }

        vrCamera.tag = "MainCamera";
        vrCamera.stereoTargetEye = StereoTargetEyeMask.Both;

        if (rig.GetComponentInChildren<AudioListener>(true) == null)
            vrCamera.gameObject.AddComponent<AudioListener>();

        if (disableLocomotion)
        {
            // Movement providers, plus the teleport arcs on each controller.
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t != rig.transform && (t.name.Contains("Locomotion") || t.name.Contains("Teleport")))
                    t.gameObject.SetActive(false);
            }
        }

        // Everything under Camera Offset except the camera itself = controllers (and their helpers).
        controllerObjects.Clear();
        Transform offset = xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null
            ? xrOrigin.CameraFloorOffsetObject.transform
            : vrCamera.transform.parent;
        if (offset != null)
        {
            foreach (Transform child in offset)
            {
                if (!vrCamera.transform.IsChildOf(child) && child.gameObject.activeSelf)
                    controllerObjects.Add(child.gameObject);
            }
        }

        // The controllers register with the EventSystem that is current when they switch on,
        // so the persistent one must exist before the rig is activated.
        EnsureXREventSystem();

        // Activate the rig now that it is configured.
        rig.transform.SetParent(null, false);
        DontDestroyOnLoad(rig);
        Destroy(holder);

        return true;
    }

    void SetControllersVisible(bool visible)
    {
        foreach (var go in controllerObjects)
        {
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }
    }

    // ---------------------------------------------------------------- per-scene setup

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!vrActive || starting) return;   // StartVR handles the first scene itself
        RunSceneSetup(scene);                // additive loads too: their cameras and canvases need the same treatment
    }

    void RunSceneSetup(Scene scene)
    {
        if (setupRoutine != null) StopCoroutine(setupRoutine);
        setupRoutine = StartCoroutine(SetupScene(scene));
    }

    IEnumerator SetupScene(Scene scene)
    {
        // Cameras first, in the same frame, so the participant never sees a desktop view.
        DisableDesktopCameras(scene);
        EnsureXREventSystem();

        // One frame so the scene's own Start() methods have run (they may switch UI on).
        yield return null;

        // Nothing here waits for tracking: panels are placed from the scene's start pose, not the head.
        // An exception must never leave setup "pending", or the 4x-a-second UI check would stop for good.
        try
        {
            PlaceRigAtAnchor();
            ConvertCanvases(scene);   // includes panels that are still hidden, so they appear in place
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VR] Setting up scene '{scene.name}' failed: {e}");
        }
        setupRoutine = null;
        SafeMaintainUI();             // anything else that is active right now, from any scene
        LogUIReport(scene);

        // If the headset pose wasn't valid yet, put the view in the right place as soon as it is.
        if (!HeadPoseValid())
        {
            float waited = 0f;
            while (!HeadPoseValid() && waited < trackingWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (HeadPoseValid())
                PlaceRigAtAnchor();
            else if (!warnedNoTracking)
            {
                warnedNoTracking = true;
                Debug.LogWarning($"[VR] Couldn't confirm headset tracking. If the view is off-centre, press {recenterKey}.");
            }
        }
    }

    /// <summary>
    /// True once the headset is giving a real pose. Runtimes report this differently (VIVE Streaming
    /// doesn't set isTracked), so accept any of: tracking-state flags, isTracked, or a non-zero pose.
    /// </summary>
    bool HeadPoseValid()
    {
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (head.isValid)
        {
            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out InputTrackingState state) &&
                (state & (InputTrackingState.Position | InputTrackingState.Rotation)) != 0)
                return true;

            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked)
                return true;

            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 p) && p != Vector3.zero)
                return true;
        }

        // Last resort: the tracked camera has moved off its default pose.
        return vrCamera != null &&
               (vrCamera.transform.localPosition != Vector3.zero || vrCamera.transform.localRotation != Quaternion.identity);
    }

    void DisableDesktopCameras(Scene scene)
    {
        haveAnchor = false;
        Camera fallback = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam == vrCamera || cam.targetTexture != null) continue; // leave render-texture cameras alone

                if (!haveAnchor && cam.CompareTag("MainCamera"))
                {
                    SetAnchor(cam.transform);
                }
                else if (fallback == null && cam.isActiveAndEnabled)
                {
                    fallback = cam;
                }

                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        if (!haveAnchor && fallback != null) SetAnchor(fallback.transform);
        if (!haveAnchor)
            Debug.LogWarning($"[VR] Scene '{scene.name}' has no desktop camera; keeping the participant where they were.");
    }

    void SetAnchor(Transform t)
    {
        haveAnchor = true;
        anchorPosition = t.position;
        anchorYaw = t.eulerAngles.y;
    }

    /// <summary>Puts the participant's eyes where the scene's desktop camera was, facing the same way.</summary>
    void PlaceRigAtAnchor()
    {
        if (!haveAnchor) return;

        if (xrOrigin != null)
        {
            Vector3 forward = Quaternion.Euler(0f, anchorYaw, 0f) * Vector3.forward;
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward);
            xrOrigin.MoveCameraToWorldLocation(anchorPosition);
        }
        else
        {
            // Rig without XROrigin: shift the root so the camera lands on the anchor.
            Transform head = vrCamera.transform;
            float yawDelta = anchorYaw - head.eulerAngles.y;
            rig.transform.RotateAround(head.position, Vector3.up, yawDelta);
            rig.transform.position += anchorPosition - head.position;
        }
    }

    /// <summary>Re-centres the participant to the scene's start pose (position + facing). Also on the Recenter key.</summary>
    [ContextMenu("Recenter now")]
    public void Recenter()
    {
        if (!vrActive) return;
        if (!haveAnchor) SetAnchorFromHead();
        PlaceRigAtAnchor();
        Debug.Log("[VR] View re-centred.");
    }

    void SetAnchorFromHead()
    {
        haveAnchor = true;
        anchorPosition = vrCamera.transform.position;
        anchorYaw = vrCamera.transform.eulerAngles.y;
    }

    // ---------------------------------------------------------------- UI

    /// <summary>Converts the scene's canvases; returns true if any has a button, slider or other control.</summary>
    bool ConvertCanvases(Scene scene)
    {
        bool hasInteractiveUI = false;
        if (!haveAnchor) SetAnchorFromHead();

        Quaternion facing = Quaternion.Euler(0f, anchorYaw, 0f);
        Vector3 panelCentre = anchorPosition + facing * Vector3.forward * uiDistance + Vector3.up * uiHeightOffset;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas && canvas.transform.parent != null &&
                    canvas.transform.parent.GetComponentInParent<Canvas>(true) != null)
                    continue; // nested canvas: follows its parent

                if (canvas.GetComponent<VRKeepOnDesktop>() != null) continue;

                if (canvas.renderMode != RenderMode.WorldSpace)
                    MakeWorldPanel(canvas, panelCentre, facing);
                else
                    canvas.worldCamera = vrCamera;

                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                if (canvas.GetComponentInChildren<Selectable>(true) != null)
                    hasInteractiveUI = true;
            }
        }
        return hasInteractiveUI;
    }

    /// <summary>
    /// Runs 4x a second: converts any active screen-space canvas (from any scene, including persistent
    /// ones), keeps the EventSystem on controller input, and shows the controllers only while there is
    /// something to click.
    /// </summary>
    void MaintainUI()
    {
        if (!haveAnchor) SetAnchorFromHead();
        Quaternion facing = Quaternion.Euler(0f, anchorYaw, 0f);
        Vector3 panelCentre = anchorPosition + facing * Vector3.forward * uiDistance + Vector3.up * uiHeightOffset;

        bool hasInteractiveUI = false;

#if UNITY_2022_2_OR_NEWER
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        Canvas[] canvases = FindObjectsOfType<Canvas>();
#endif
        foreach (var canvas in canvases)
        {
            if (!canvas.isRootCanvas || canvas.GetComponent<VRKeepOnDesktop>() != null) continue;

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                int id = canvas.GetInstanceID();
                string was = canvas.renderMode.ToString();
                MakeWorldPanel(canvas, panelCentre, facing);

                if (convertedCanvases.Add(id))
                {
                    Debug.Log($"[VR] Moved canvas '{canvas.name}' (scene '{canvas.gameObject.scene.name}', was {was}) in front of the participant.");
                }
                else if (warnedResetCanvases.Add(id))
                {
                    Debug.LogWarning($"[VR] Canvas '{canvas.name}' keeps being set back to {was} by another script. " +
                                     "Tell me which script sets canvas.renderMode so it can check VRModeManager.IsVR.");
                }
            }

            canvas.worldCamera = vrCamera;
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            if (canvas.GetComponentInChildren<Selectable>(false) != null)
                hasInteractiveUI = true;
        }

        EnsureXREventSystem();
        SetControllersVisible(!hideControllersWithoutUI || hasInteractiveUI);
    }

    /// <summary>
    /// One EventSystem for the whole VR session. The controllers register with its input module once,
    /// when the rig is switched on. If each scene brought its own EventSystem, the controllers would stay
    /// registered with the previous scene's (destroyed) module and stop clicking after a scene change.
    /// So: create a persistent one, and switch off every scene EventSystem while in VR.
    /// </summary>
    void EnsureXREventSystem()
    {
#if UNITY_2022_2_OR_NEWER
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        EventSystem[] systems = FindObjectsOfType<EventSystem>();
#endif
        // Switch the scene ones off first, so the persistent one becomes EventSystem.current.
        foreach (var es in systems)
        {
            if (es == vrEventSystem || !es.enabled) continue;
            foreach (var module in es.GetComponents<BaseInputModule>()) module.enabled = false;
            es.enabled = false;
            if (disabledEventSystems.Add(es.GetInstanceID()))
                Debug.Log($"[VR] Scene EventSystem '{es.name}' ({es.gameObject.scene.name}) switched off; using the persistent VR one.");
        }

        if (vrEventSystem == null)
        {
            var go = new GameObject("EventSystem (VR, persistent)");
            DontDestroyOnLoad(go);
            vrEventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();   // controller rays + mouse
            Debug.Log("[VR] Created persistent EventSystem for controller input.");
        }
        else if (!vrEventSystem.enabled)
        {
            vrEventSystem.enabled = true;
        }
    }

    void MakeWorldPanel(Canvas canvas, Vector3 centre, Quaternion facing)
    {
        var rt = (RectTransform)canvas.transform;

        // Keep the layout the canvas was designed for.
        Vector2 size = rt.sizeDelta;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            size = scaler.referenceResolution;
        if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = vrCamera;

        rt.sizeDelta = size;
        rt.localScale = Vector3.one * (uiWidthMetres / size.x);
        rt.SetPositionAndRotation(centre, facing);
    }

    // ---------------------------------------------------------------- input

    bool RecenterPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(recenterKey);
#elif ENABLE_INPUT_SYSTEM
        // Input System only: the Recenter Key field is ignored and R is used.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
}
