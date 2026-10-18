using UnityEngine;

namespace CoinCatcher
{
    /// <summary>World-space edges of what an orthographic camera shows.</summary>
    public static class CameraBounds
    {
        // orthographicSize is half the visible HEIGHT; the width follows the screen's aspect ratio,
        // so a wider phone shows more world to the left and right. Always ask the camera, never hard-code.
        public static float HalfWidth(Camera camera) => camera.orthographicSize * camera.aspect;

        public static float Left(Camera camera) => camera.transform.position.x - HalfWidth(camera);
        public static float Right(Camera camera) => camera.transform.position.x + HalfWidth(camera);
        public static float Top(Camera camera) => camera.transform.position.y + camera.orthographicSize;
        public static float Bottom(Camera camera) => camera.transform.position.y - camera.orthographicSize;
    }
}
