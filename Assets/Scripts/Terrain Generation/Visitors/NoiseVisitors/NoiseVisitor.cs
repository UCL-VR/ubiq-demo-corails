﻿using System.Linq;
using UnityEngine;
using Utils;

namespace Terrain_Generation.Visitors.NoiseVisitors {
    public abstract class NoiseVisitor : Visitor {
        private static readonly int ratio = 10;
        private readonly float largest;
        private readonly CubicNoise noise;

        private float threshold;


        protected NoiseVisitor() {
            int seed = (int) RandomNumberGenerator.Instance.Generate(int.MinValue, int.MaxValue);
            Debug.Log("Noise Visitor: " + seed);
            noise = new CubicNoise(seed, octave);

            float[] samples = new float[10 * ratio * 10 * ratio];
            for (int x = 0; x < 10 * ratio; x++)
            for (int y = 0; y < 10 * ratio; y++)
                samples[x + y * 10 * ratio] = Mathf.Pow(noise.Sample(x, y), power);

            samples = samples.OrderBy(a => a).ToArray();

            largest = samples[samples.Length - 1];
        }

        public abstract float power { get; }

        // Number of objects to spawn per positive y
        public abstract float number { get; }
        public abstract int octave { get; }

        public abstract float offset { get; }
        public abstract float offsetMultiply { get; }

        public override void Generate(Chunk[,] chunks) {
            var randomNumberGenerator = RandomNumberGenerator.Instance;

            int toSpawn = (int) (chunks.GetLength(1) * number);
            int spawned = 0;
            while (spawned < toSpawn) {
                int positionX = (int) randomNumberGenerator.Generate(0, chunks.GetLength(0) * ratio - 1);
                int positionY = (int) randomNumberGenerator.Generate(0, chunks.GetLength(1) * ratio - 1);

                if (Mathf.Pow(noise.Sample(positionX, positionY), power) >
                    (int) randomNumberGenerator.Generate(0, largest * 0.2f - 1)) {
                    int chunkX = positionX / 10;
                    int chunkY = positionY / 10;

                    if (chunks[chunkX, chunkY].properties.ContainsKey("Path")) continue;

                    ChunkObject chunkObject = Place(chunks[chunkX, chunkY]);

                    if (chunkObject != null) {
                        chunkObject.x = (float) positionX % 10 * offsetMultiply / 10 - offset;
                        chunkObject.y = (float) positionY % 10 * offsetMultiply / 10 - offset;

                        chunks[chunkX, chunkY].AddObject(chunkObject);
                        spawned += 1;
                    }
                }
            }
        }

        protected abstract ChunkObject Place(Chunk chunk);
    }
}