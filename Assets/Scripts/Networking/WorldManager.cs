using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ResourceDrops;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Spawning;
using UnityEngine;
using UnityEngine.Events;
using Terrain = Terrain_Generation.Terrain;

namespace Networking {
    public class WorldManager : MonoBehaviour 
        {
        // This list contains all the environment assets that have been harvested since the start
        private List<string> destroyedObjects = new List<string>();

        // This list contains all the resources spawned as a result of harvesting
        private Dictionary<string, int> spawnedObjects = new Dictionary<string, int>();

        private readonly Queue<Tuple<string, GameObject>> wmEventQueue = new Queue<Tuple<string, GameObject>>();

        public NetworkId NetworkId => new NetworkId(2815);

        private NetworkContext netContext;

        private bool ready = true; // ready to start processing network events (or just in single-player)

        private RoomClient roomClient;
        private Scoring.ScoringEvent onScoreEvent;

        private Terrain terrainGenerationComponent;
        private int worldInitState;

        public PrefabCatalogue worldManagerSpawnable;

        // Start is called before the first frame update
        private void Start() {
           
            netContext = NetworkScene.Register(this);
            
            onScoreEvent = GameObject.Find("Scoring").GetComponent<Scoring>().OnScoreEvent;

            roomClient = RoomClient.Find(this);
            roomClient.OnPeerAdded.AddListener(SendWorldStateToNewPeer);
            roomClient.OnJoinedRoom.AddListener(UpdateInitState);
            terrainGenerationComponent = GameObject.Find("Generation").GetComponent<Terrain>();

            Debug.Log("[WorldManager] Hello world!");
        }

        private void Update() {
            ProcessQueueEvent();
        }

        private IEnumerator WorldManagerSync() {
            Debug.LogWarning("[ONJOINEDROOM] Joined room, waiting for peers...");
            ready = false;
            const int timeoutMax = 5; // give 500ms for initializing world sync
            bool roomHasPeers = false;
            for (int timeoutTicker = 0; timeoutTicker < timeoutMax; timeoutTicker++) {
                if (roomClient.Peers.Any()) // waiting for peers to join within timeout period
                {
                    roomHasPeers = true;
                    Debug.Log("Room has peer(s)");
                    break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            if (roomHasPeers) {
                Debug.LogWarning("[ONJOINEDROOM] room has peers, waiting for sync message");
                yield break; // don't destroy terrain, peer(s) exist so wait for initState to be sent by someone else
            }

            RegenerateTerrain(terrainGenerationComponent.initState);
            ready = true; // we just joined (created) an empty room, we get to set the room's seed.
        }

        private void RegenerateTerrain(int initState) {
            worldInitState = terrainGenerationComponent.Generate(initState);
            wmEventQueue.Clear();
        }

        private void SendWorldStateToNewPeer(IPeer newPeer) {
            Debug.Log($"New peer joined: {newPeer.uuid}");
            int mySuffix = roomClient.Me.uuid.Last();

            // use last character of UUID as integer, lowest integer in room sends new updates to new peer
            bool doSend = roomClient.Peers.Where(peer => peer != newPeer).Select(peer => peer.uuid.Last())
                .All(peerSuffix => peerSuffix > mySuffix);

            if (!doSend || !ready) return;

            Debug.LogWarning("Sending World State to New Peer");

            var m = new WorldManagerMessage {
                WorldInitState = worldInitState
            };
            netContext.SendJson(m);

            foreach (var item in destroyedObjects) {
                netContext.SendJson(new WorldManagerMessage() {
                    ToRemove = item
                });
            }

            foreach (var item in spawnedObjects) {
                netContext.SendJson(new WorldManagerMessage() {
                    ToCreateName = item.Key,
                    ToCreateIndex = item.Value
                });
            }

            foreach (var item in spawnedObjects) {
                var g = GameObject.Find(item.Key);
                if (g) {
                    g.GetComponent<ResourceDropManager>().ForceSendPositionUpdate();
                }
            }
        }

        private void UpdateInitState(IRoom newRoom) {
            StartCoroutine(WorldManagerSync());
        }

        private struct WorldManagerMessage {
            public int WorldInitState;
            public string ToRemove;
            public int ToCreateIndex;
            public string ToCreateName;
            public Vector3 ToCreatePosition;
        }

        // This method processes the wmEventQueue, which is populated locally
        private void DoDestroyAndReplace() {
            if (!ready || wmEventQueue.Count == 0) {
                return;
            }

            (string toDestroy, GameObject toSpawn) = wmEventQueue.Dequeue();

            var msg = new WorldManagerMessage();
            msg.ToCreateIndex = -1;
            msg.ToRemove = toDestroy;

            var previous = GameObject.Find(toDestroy);
            var position = previous.transform.position;

            if (previous.TryGetComponent(out ResourceDropManager resourceDropManager)) {
                onScoreEvent.Invoke(resourceDropManager.type == "wood"
                    ? ScoreEventType.WoodPickUp
                    : ScoreEventType.StonePickUp);
                spawnedObjects.Remove(previous.name);
            }
            else {
                destroyedObjects.Add(previous.name);
            }

            GameObject.Destroy(previous);

            if (toSpawn != null) {
                var replacement = GameObject.Instantiate(toSpawn);
                replacement.transform.position = position;
                replacement.name = Guid.NewGuid().ToString();

                msg.ToCreateIndex = worldManagerSpawnable.prefabs.IndexOf(toSpawn);
                msg.ToCreatePosition = position;
                msg.ToCreateName = replacement.name;

                spawnedObjects.Add(msg.ToCreateName, msg.ToCreateIndex);
            }

            netContext.SendJson(msg);            
        }

        private void ProcessQueueEvent() // process one queue event
        {
            DoDestroyAndReplace();
        }

        public void UpdateWorld(GameObject before, GameObject after) {
            wmEventQueue.Enqueue(new Tuple<string, GameObject>(before.name, after)); // event queue will be handled by DoDestroyAndReplace
        }

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message) {
            var wmm = message.FromJson<WorldManagerMessage>();

            if (wmm.WorldInitState > 0 && !ready) {
                // if world already ready, ignore message
                Debug.Log($"Processing initial world sync message, seed: {wmm.WorldInitState}");
                RegenerateTerrain(wmm.WorldInitState);
                ready = true;
            }

            var toDestroy = GameObject.Find(wmm.ToRemove);
            if (toDestroy) {
                if (toDestroy.TryGetComponent(out ResourceDropManager resourceDropManager)) {
                    spawnedObjects.Remove(toDestroy.name);
                }
                else {
                    destroyedObjects.Add(toDestroy.name);
                }
                GameObject.Destroy(toDestroy);
            }

            if (wmm.ToCreateIndex >= 0) {
                var spawned = GameObject.Instantiate(worldManagerSpawnable.prefabs[wmm.ToCreateIndex]);
                spawned.name = wmm.ToCreateName;
                spawned.transform.position = wmm.ToCreatePosition;
            }
        }
    }
}