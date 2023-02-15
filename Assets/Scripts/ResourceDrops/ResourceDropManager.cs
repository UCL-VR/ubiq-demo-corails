using Networking;
using Ubiq.Messaging;
using Ubiq.XR;
using UnityEngine;

namespace ResourceDrops {
    public class ResourceDropManager : MonoBehaviour, IGraspable {
        public bool owner;
        public string type;
        private Vector3 lastPublishedPosition;

        private NetworkContext ctx;
        private Transform follow;
        private Rigidbody rb;

        public void Start() {
            ctx = NetworkScene.Register(this);
            gameObject.tag = "ResourceDrop";
            rb = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        private void Update() {
            // if (!owner) return;
            if (follow != null)
            {
                Vector3 controllerPosition = follow.transform.position;
                transform.position = new Vector3(controllerPosition.x, controllerPosition.y - (float) 0.1,
                    controllerPosition.z);
                transform.rotation = follow.transform.rotation;
                transform.Rotate(50, 0, 0);
            }
            if (Vector3.Distance(lastPublishedPosition, transform.position) <= 0.2) {
                return;
            }
            SendPositionUpdate();
        }

        public void Grasp(Hand controller) {
            follow = controller.transform;
            owner = true;
            rb.isKinematic = true;
        }

        public void Release(Hand controller) {
            follow = null;
            owner = false;
            if (rb == null) return;
            rb.isKinematic = false;
        }

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message) {
            var msg = message.FromJson<TransformMessage>();
            transform.position = msg.position;
            transform.rotation = msg.rotation;
            lastPublishedPosition = msg.position;
        }

        public void ForceSendPositionUpdate() {
            SendPositionUpdate();
        }

        private void SendPositionUpdate() {
            ctx.SendJson(new TransformMessage(transform));
        }
    }
}