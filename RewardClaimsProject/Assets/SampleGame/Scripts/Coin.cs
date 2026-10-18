using System;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// One falling coin. Reports whether the basket caught it or it fell off the screen, then destroys itself.
    /// </summary>
    // Unity only sends trigger messages when at least one of the two colliders has a Rigidbody2D.
    // Kinematic means "moved by us, not by gravity or collisions": the physics engine still tracks it
    // and detects overlaps, but nothing pushes it around.
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Coin : MonoBehaviour
    {
        private Rigidbody2D body;
        private float missBelowY;
        private Action<Coin> onCaught;
        private Action<Coin> onMissed;
        private bool finished;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        /// <summary>Starts the fall. The spawner calls this right after creating the coin.</summary>
        public void Launch(float fallSpeed, float missBelowY, Action<Coin> onCaught, Action<Coin> onMissed)
        {
            this.missBelowY = missBelowY;
            this.onCaught = onCaught;
            this.onMissed = onMissed;

            // A kinematic body keeps a constant velocity, so the physics step moves the coin for us.
            body.linearVelocity = Vector2.down * fallSpeed;
        }

        private void Update()
        {
            if (transform.position.y < missBelowY)
                Finish(onMissed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<Basket>(out _))
                Finish(onCaught);
        }

        private void Finish(Action<Coin> report)
        {
            // Destroy only happens at the end of the frame, so guard against reporting twice
            // (e.g. caught and missed in the same frame).
            if (finished)
                return;

            finished = true;
            report?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
