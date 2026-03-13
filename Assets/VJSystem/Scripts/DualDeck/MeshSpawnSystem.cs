using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace VJSystem
{
    /// <summary>
    /// Spawns FBX meshes on a stage via a random-walk cursor.
    /// One instance per stage — each has its own independent cursor.
    ///
    /// Groups 0-3 correspond to MF64 rows 5-8.
    ///   Cols 1-7  → SpawnInGroup(groupIndex)
    ///   Col  8    → ClearGroup(groupIndex)
    ///
    /// Control row (MF64 row 4):
    ///   Col 1 → Scramble()    — rescatter all objects to new random positions
    ///   Col 2 → ResetCursor() — return walk cursor to stage origin
    /// </summary>
    public class MeshSpawnSystem : MonoBehaviour
    {
        [Header("Stage")]
        public Vector3 stageOrigin;

        [Header("Meshes & Materials")]
        public Mesh[]     meshes;
        public Material[] materials;

        [Header("Walk Settings")]
        [Tooltip("Base step distance per spawn")]
        public float stepMagnitude  = 1.5f;
        [Tooltip("Random ± variance added to step distance")]
        public float stepVariance   = 0.5f;
        [Tooltip("Base height of spawned objects above stage origin")]
        public float spawnHeight    = 0.5f;
        [Tooltip("Additional random height range")]
        public float spawnHeightRange = 2.5f;
        [Tooltip("Max XZ distance from origin before cursor wraps inward")]
        public float walkRadius     = 5f;

        [Header("Scale")]
        public float minScale        = 0.3f;
        public float maxScale        = 1.5f;
        public float scaleInDuration = 0.3f;
        public float scaleOutDuration = 0.4f;

        [Header("Rotation Speed (deg/s)")]
        public float rotationSpeedMin = 5f;
        public float rotationSpeedMax = 40f;

        // Groups 0-3 correspond to rows 5-8
        readonly List<SpawnedMeshObject>[] _groups =
        {
            new List<SpawnedMeshObject>(),
            new List<SpawnedMeshObject>(),
            new List<SpawnedMeshObject>(),
            new List<SpawnedMeshObject>()
        };

        Vector3   _cursor;
        Transform _spawnRoot;

        // 4 groups × 7 cols = 28 button slots, each assigned a fixed material.
        // Reassigned randomly on Scramble().
        Material[] _buttonMaterials;

        void Awake()
        {
            _cursor = stageOrigin;

            var rootGO = new GameObject("SpawnedMeshes");
            _spawnRoot = rootGO.transform;
            _spawnRoot.SetParent(transform);

            AssignButtonMaterials();
        }

        void AssignButtonMaterials()
        {
            _buttonMaterials = new Material[28]; // 4 groups × 7 cols
            if (materials == null || materials.Length == 0) return;
            for (int i = 0; i < 28; i++)
                _buttonMaterials[i] = materials[Random.Range(0, materials.Length)];
        }

        // Remove null entries caused by external destroy
        void PruneGroup(int idx)
        {
            var list = _groups[idx];
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null) list.RemoveAt(i);
        }

        // ------------------------------------------------------------------ //

        /// <summary>Spawn one mesh at current cursor position, then advance cursor.
        /// buttonCol 1-7 selects the fixed material assigned to that button slot.</summary>
        public void SpawnInGroup(int groupIndex, int buttonCol)
        {
            if (groupIndex < 0 || groupIndex > 3) return;
            if (meshes == null || meshes.Length == 0) return;
            if (materials == null || materials.Length == 0) return;

            PruneGroup(groupIndex);

            // Advance cursor with a 2D random step (keep mostly horizontal)
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist  = stepMagnitude + Random.Range(-stepVariance, stepVariance);
            _cursor.x  += dir.x * dist;
            _cursor.z  += dir.y * dist;
            _cursor.y   = stageOrigin.y + spawnHeight + Random.Range(0f, spawnHeightRange);

            // Wrap cursor back inside radius
            Vector2 xzOffset = new Vector2(_cursor.x - stageOrigin.x, _cursor.z - stageOrigin.z);
            if (xzOffset.magnitude > walkRadius)
            {
                xzOffset   = Random.insideUnitCircle * (walkRadius * 0.5f);
                _cursor.x  = stageOrigin.x + xzOffset.x;
                _cursor.z  = stageOrigin.z + xzOffset.y;
            }

            // Create GameObject
            var go = new GameObject($"Mesh_G{groupIndex}");
            go.transform.SetParent(_spawnRoot);
            go.transform.position   = _cursor;
            go.transform.rotation   = Random.rotation;
            go.transform.localScale = Vector3.zero;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = meshes[Random.Range(0, meshes.Length)];

            int slot = groupIndex * 7 + Mathf.Clamp(buttonCol - 1, 0, 6);
            var mat  = (_buttonMaterials != null && _buttonMaterials[slot] != null)
                       ? _buttonMaterials[slot]
                       : materials[Random.Range(0, materials.Length)];

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var smo = go.AddComponent<SpawnedMeshObject>();
            smo.rotationSpeed = Random.Range(rotationSpeedMin, rotationSpeedMax);

            go.transform.DOScale(Vector3.one * Random.Range(minScale, maxScale), scaleInDuration)
                .SetEase(Ease.OutQuad);

            _groups[groupIndex].Add(smo);
        }

        /// <summary>Scale-out and destroy all objects in this group (staggered).</summary>
        public void ClearGroup(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex > 3) return;
            PruneGroup(groupIndex);

            var list = _groups[groupIndex];
            float stagger = 0.02f;
            for (int i = 0; i < list.Count; i++)
            {
                var smo   = list[i];
                float d   = i * stagger;
                DOVirtual.DelayedCall(d, () => smo?.ScaleOut(scaleOutDuration));
            }
            list.Clear();
        }

        /// <summary>Animate all spawned objects to new random positions and reassign button materials.</summary>
        public void Scramble()
        {
            AssignButtonMaterials();

            for (int g = 0; g < _groups.Length; g++)
            {
                PruneGroup(g);
                foreach (var smo in _groups[g])
                {
                    Vector3 newPos = stageOrigin + new Vector3(
                        Random.Range(-walkRadius, walkRadius),
                        spawnHeight + Random.Range(0f, spawnHeightRange),
                        Random.Range(-walkRadius, walkRadius));
                    smo.MoveTo(newPos, 0.5f);
                }
            }
        }

        /// <summary>Reset walk cursor to stage origin.</summary>
        public void ResetCursor()
        {
            _cursor = stageOrigin;
        }
    }
}
