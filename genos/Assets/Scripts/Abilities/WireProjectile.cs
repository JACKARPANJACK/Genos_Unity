using UnityEngine;
using System;

namespace Climbing.Abilities
{
    public class WireProjectile : MonoBehaviour
    {
        public float speed = 80f; // High speed
        public float maxDistance = 60f;
        public LineRenderer lineRenderer;
        
        private Transform owner;
        private Transform socket;
        private bool isLaunched = false;
        private bool isAttached = false;
        private Transform attachedTransform;
        private Vector3 attachedLocalPos;
        private Vector3 attachedWorldPos;

        private System.Action<Transform, Vector3> onImpact;

        public void Launch(Transform owner, Transform socket, System.Action<Transform, Vector3> onImpact)
        {
            this.owner = owner;
            this.socket = socket;
            this.onImpact = onImpact;
            isLaunched = true;
            transform.SetParent(null);
        }

        void Update()
        {
            if (!isLaunched) return;

            if (!isAttached)
            {
                transform.Translate(Vector3.forward * speed * Time.deltaTime);
                if (Vector3.Distance(owner.position, transform.position) > maxDistance)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                if (attachedTransform != null)
                    transform.position = attachedTransform.TransformPoint(attachedLocalPos);
                else
                    transform.position = attachedWorldPos;
            }

            UpdateLine();
        }

        private void UpdateLine()
        {
            if (lineRenderer != null && socket != null)
            {
                lineRenderer.SetPosition(0, socket.position);
                lineRenderer.SetPosition(1, transform.position);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isAttached || !isLaunched) return;
            if (other.transform.root == owner.root) return;

            Attach(other);
        }

        private void Attach(Collider other)
        {
            isAttached = true;
            attachedTransform = other.transform;
            attachedLocalPos = attachedTransform.InverseTransformPoint(transform.position);
            attachedWorldPos = transform.position;

            onImpact?.Invoke(attachedTransform, transform.position);
        }

        public void Detach()
        {
            Destroy(gameObject);
        }
    }
}
