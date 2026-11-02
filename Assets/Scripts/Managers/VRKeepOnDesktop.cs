using UnityEngine;

/// <summary>
/// Add to a Canvas that only the RA should see on the PC monitor (e.g. a debug HR readout or an
/// operator Start button). VRModeManager will leave it as a screen-space canvas instead of moving
/// it in front of the participant.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRKeepOnDesktop : MonoBehaviour { }
