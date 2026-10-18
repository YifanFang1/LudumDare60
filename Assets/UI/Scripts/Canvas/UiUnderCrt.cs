using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes the CRT filter cover the UI too.
// A Screen Space Overlay canvas draws after post-processing, so the CRT can't
// touch it. This renders the canvas through its own overlay camera that is
// stacked on the main camera; the CRT pass runs on the last camera in the
// stack, so it then filters game and UI together.
// MatchScreens adds this automatically (tick "CRT Over UI" there to enable).
[DisallowMultipleComponent]
public class UiUnderCrt : MonoBehaviour
{
    Canvas canvas;
    Camera uiCam;
    Camera baseCam;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    void OnDisable()
    {
        Detach();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        Camera main = Camera.main;
        if (main == null) return;
        if (uiCam != null && baseCam == main && canvas.worldCamera == uiCam) return;
        Attach(main);
    }

    void Attach(Camera main)
    {
        Detach();
        var mainData = main.GetUniversalAdditionalCameraData();
        if (mainData.renderType != CameraRenderType.Base) return;

        if (uiCam == null)
        {
            var go = new GameObject("UI Camera (CRT)");
            go.transform.SetParent(transform.parent, false);
            uiCam = go.AddComponent<Camera>();
            uiCam.orthographic = true;
            uiCam.clearFlags = CameraClearFlags.Depth;
            uiCam.cullingMask = 1 << gameObject.layer;   // the canvas's layer only; canvases are culled by layer
            uiCam.nearClipPlane = 0.1f;
            uiCam.farClipPlane = 10f;
            uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        }

        baseCam = main;
        mainData.cameraStack.Add(uiCam);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCam;
        canvas.planeDistance = 1f;
    }

    void Detach()
    {
        if (baseCam != null && uiCam != null)
            baseCam.GetUniversalAdditionalCameraData().cameraStack.Remove(uiCam);
        baseCam = null;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCam != null)
        {
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (uiCam != null) { Destroy(uiCam.gameObject); uiCam = null; }
    }
}
