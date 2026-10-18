using System.Collections.Generic;
using UnityEngine;

// Layered stage background with parallax. Put it on any GameObject in the Fight scene and add
// layers in the inspector, farthest first (sky, then mountains, then trees...).
//
// Set up each layer as it should look with the default camera (x 0, y 3, size 5: the whole
// arena). When the camera follows the fighters (FightCamera), far layers move less than near
// ones, which is what makes the stage feel deep.
//
//   Follow Camera  0 = at the fighters' depth (moves exactly like the stage)
//                  1 = infinitely far (stays put on screen, like the sky)
//   Tile           repeat the sprite left and right so the layer never ends
//   Drift Speed    units per second the layer slides on its own (clouds, fog)
//
// It only reads the camera, never the sim, so it cannot affect gameplay. It also runs outside
// Play mode, so you can line layers up in the Game view without pressing Play.
//
// With Only During Match ticked it stays hidden in Play mode until a fight starts (behind the
// menus, lobby and character select), and hides again when the match ends.
[ExecuteAlways]
[DefaultExecutionOrder(100)]   // after FightCamera has moved the camera this frame
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string name = "Layer";
        public Sprite sprite;
        [Range(0f, 1f)] public float followCamera = 0.5f;
        public bool tile = true;
        public float driftSpeed;
        public float x;                       // where the layer is centred (when not tiled)
        public float y;                       // height of the sprite's bottom edge
        public float scale = 1f;
        public Color tint = Color.white;
    }

    public List<Layer> layers = new List<Layer>();

    [Tooltip("Optional. If the background looks black, use an Unlit sprite material.")]
    public Material material;
    public int sortingOrder = -100;           // first layer; each next layer is drawn one higher (fighters are 0)
    [Tooltip("In Play mode, only show the background while a fight is on screen (not behind the menus). Always shown outside Play mode.")]
    public bool onlyDuringMatch = true;

    [Header("The camera the layers were set up with (WorldView's default)")]
    public float referenceCameraY = 3f;
    public float referenceSize = 5f;

    readonly List<List<SpriteRenderer>> tiles = new List<List<SpriteRenderer>>();
    readonly List<float> drift = new List<float>();
    Transform root;
    bool dirty = true;
    IMatchSession match;   // found on first use; null in scenes without one (then always shown)

    void OnEnable() { dirty = true; }
    void OnDisable() { DestroyTiles(); }
    // Inspector edits: rebuild on the next update. Layers added with the + button start with every
    // field zeroed (Unity skips the defaults above), which would make them invisible, so fix those.
    void OnValidate()
    {
        dirty = true;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].scale <= 0f) layers[i].scale = 1f;
            if (layers[i].tint.a <= 0f) layers[i].tint = Color.white;
        }
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (dirty || root == null || tiles.Count != layers.Count) Rebuild();

        if (!ShouldShow())
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                for (int t = 0; t < tiles[i].Count; t++) tiles[i][t].enabled = false;
            }
            return;
        }

        float zoom = cam.orthographicSize / referenceSize;
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 camPos = cam.transform.position;

        for (int i = 0; i < layers.Count; i++)
        {
            Layer layer = layers[i];
            List<SpriteRenderer> pool = tiles[i];
            if (layer.sprite == null)
            {
                for (int t = 0; t < pool.Count; t++) pool[t].enabled = false;
                continue;
            }

            if (Application.isPlaying) drift[i] += layer.driftSpeed * Time.deltaTime;

            // Where the layer is in the world, and where it would be if it were painted on the
            // screen. Follow Camera blends between the two.
            float f = layer.followCamera;
            float worldX = layer.x + drift[i];
            float worldY = layer.y;
            float screenX = camPos.x + worldX * zoom;
            float screenY = camPos.y + (worldY - referenceCameraY) * zoom;
            float scale = layer.scale * Mathf.Lerp(1f, zoom, f);   // far layers do not zoom in

            float anchorX = Mathf.Lerp(worldX, screenX, f);
            float bottomY = Mathf.Lerp(worldY, screenY, f);

            Bounds b = layer.sprite.bounds;
            float width = b.size.x * scale;

            // Tiled: enough copies to cover the screen plus one spare on each side.
            int first = 0, count = 1;
            if (layer.tile && width > 0.001f)
            {
                first = Mathf.FloorToInt((camPos.x - halfWidth - anchorX) / width) - 1;
                int last = Mathf.CeilToInt((camPos.x + halfWidth - anchorX) / width) + 1;
                count = last - first + 1;
            }
            while (pool.Count < count) pool.Add(NewTile(layer.name, i));

            for (int t = 0; t < pool.Count; t++)
            {
                SpriteRenderer sr = pool[t];
                sr.enabled = t < count;
                if (!sr.enabled) continue;

                sr.sprite = layer.sprite;
                sr.color = layer.tint;
                sr.sortingOrder = sortingOrder + i;
                if (material != null) sr.sharedMaterial = material;

                float centreX = anchorX + (first + t) * width;
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                sr.transform.position = new Vector3(
                    centreX - b.center.x * scale,
                    bottomY - b.min.y * scale,
                    0f);
            }
        }
    }

    bool ShouldShow()
    {
        if (!onlyDuringMatch || !Application.isPlaying) return true;
        if (match == null) match = SessionFinder.Find();
        return match == null || match.Phase == MatchPhase.Playing;
    }

    void Rebuild()
    {
        DestroyTiles();
        dirty = false;

        GameObject go = new GameObject("ParallaxTiles");
        go.hideFlags = HideFlags.HideAndDontSave;   // generated: never saved into the scene
        root = go.transform;

        for (int i = 0; i < layers.Count; i++)
        {
            tiles.Add(new List<SpriteRenderer>());
            drift.Add(0f);
        }
    }

    SpriteRenderer NewTile(string layerName, int index)
    {
        GameObject go = new GameObject(layerName);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(root, false);
        return go.AddComponent<SpriteRenderer>();
    }

    void DestroyTiles()
    {
        tiles.Clear();
        drift.Clear();
        if (root == null) return;
        if (Application.isPlaying) Destroy(root.gameObject);
        else DestroyImmediate(root.gameObject);
        root = null;
    }
}
