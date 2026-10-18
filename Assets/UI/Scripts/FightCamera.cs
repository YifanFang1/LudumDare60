using UnityEngine;

// Fighting-game camera: during a match it follows the midpoint of the two fighters and zooms in
// when they are close, never showing past the edges of the stage. Without it the camera stays
// where WorldView puts it (the whole arena), and parallax backgrounds have nothing to react to.
//
// Put it on the same object as the match session (GameManager). It only reads the sim.
[DefaultExecutionOrder(50)]   // after WorldView, before ParallaxBackground
public class FightCamera : MonoBehaviour
{
    [Tooltip("Smallest orthographic size: how far it zooms in when the fighters are close.")]
    public float minSize = 3.5f;
    [Tooltip("Largest orthographic size. 5 shows the whole arena.")]
    public float maxSize = 5f;
    [Tooltip("Units kept between each fighter and the edge of the screen.")]
    public float padding = 2.5f;
    [Tooltip("The camera never shows past this distance from the centre. The ground is 20 units wide.")]
    public float stageHalfWidth = 10f;
    [Tooltip("How far above the bottom of the screen the floor sits.")]
    public float floorMargin = 2f;
    [Tooltip("Seconds to catch up. Lower = snappier.")]
    public float smoothTime = 0.15f;

    IMatchSession match;
    float xVelocity;
    float sizeVelocity;
    float x = float.NaN;    // our own x and size, so screen shake and zoom punch (added on top)
    float size = float.NaN; // don't nudge where we aim

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("FightCamera needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (match.Phase != MatchPhase.Playing)
        {
            if (!float.IsNaN(size) && Camera.main != null)
            {
                Camera.main.orthographicSize = maxSize;
                Camera.main.transform.position = new Vector3(0f, maxSize - floorMargin, Camera.main.transform.position.z);
            }
            x = size = float.NaN;
            xVelocity = sizeVelocity = 0f;
            return;
        }
        Camera cam = Camera.main;
        if (cam == null) return;

        float x0 = match.Sim.fighters[0].x * WorldView.Scale;
        float x1 = match.Sim.fighters[1].x * WorldView.Scale;

        // Zoom so both fighters fit with some padding, then centre between them.
        float halfNeeded = Mathf.Abs(x1 - x0) / 2f + padding;
        float targetSize = Mathf.Clamp(halfNeeded / cam.aspect, minSize, maxSize);
        bool defeated = match.Sim.roundOverTicks > 0 || match.Sim.IsOver;
        int winner = match.Sim.fighters[0].hp > 0 ? 0 : match.Sim.fighters[1].hp > 0 ? 1 : -1;
        if (defeated && winner >= 0) targetSize = 2.6f;
        if (float.IsNaN(size)) size = cam.orthographicSize;
        size = Mathf.SmoothDamp(size, targetSize, ref sizeVelocity, smoothTime);

        float limit = Mathf.Max(0f, stageHalfWidth - size * cam.aspect);
        float focusX = defeated && winner >= 0 ? (winner == 0 ? x0 : x1) : (x0 + x1) / 2f;
        float targetX = Mathf.Clamp(focusX, -limit, limit);
        if (float.IsNaN(x)) x = cam.transform.position.x;
        x = Mathf.SmoothDamp(x, targetX, ref xVelocity, smoothTime);

        cam.orthographicSize = size;
        cam.transform.position = new Vector3(x, size - floorMargin, cam.transform.position.z);
    }
}
