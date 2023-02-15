using System;
using Networking;
using ResourceDrops;
using UnityEngine;

namespace Tools
{
    public class CollectionManager : MonoBehaviour {
        public SuctionManager suctionManager;
        private GameObject parent;
        private WorldManager worldManager;

        private void Start() {
            parent = transform.parent.gameObject;
            worldManager = GameObject.Find("World Manager").GetComponent<WorldManager>();
        }

        private void OnTriggerEnter(Collider collision) {
            // only collect ResourceDrops
            if (!collision.TryGetComponent(out ResourceDropManager resourceDropManager)) return;

            bool result = resourceDropManager.type switch {
                "wood" => parent.GetComponent<VacuumManager>().AddItem("log"),
                "stone" => parent.GetComponent<VacuumManager>().AddItem("rock"),
                "rail" => parent.GetComponent<VacuumManager>().AddItem("rail"),
                _ => false
            };

            // stop sucking the object
            suctionManager.RemoveObj(new Tuple<GameObject, ResourceDropManager>(collision.gameObject, resourceDropManager));

            // if it was successfully added to inventory, destroy the object
            if (result) {
                worldManager.UpdateWorld(collision.gameObject, null); // destroy and don't spawn anything
            }
        }
    }
}