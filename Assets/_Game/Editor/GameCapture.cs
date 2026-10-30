using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Captures what the player sees — 3D view plus overlay UI (HUD, dialogue, subtitles) — into a PNG,
    /// even when the Game view is not focused (ScreenCapture needs a focused Game view). Used for
    /// automated play-testing: overlay canvases are temporarily switched to camera space for the render.
    /// </summary>
    public static class GameCapture
    {
        public static string Capture(string file, int width = 1280, int height = 720)
        {
            var cam = Camera.main;
            if (!cam) return "no camera";
            var switched = new List<(Canvas canvas, float plane)>();
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isActiveAndEnabled) continue;
                switched.Add((c, c.planeDistance));
            }
            // Higher sorting orders must end up closer to the camera.
            switched.Sort((a, b) => a.canvas.sortingOrder.CompareTo(b.canvas.sortingOrder));
            for (int i = 0; i < switched.Count; i++)
            {
                var c = switched[i].canvas;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.5f - i * 0.01f;
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = prev;

            foreach (var (c, plane) in switched)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.planeDistance = plane;
            }

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = null;
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", file));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            return full;
        }
    }
}
