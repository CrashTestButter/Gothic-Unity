using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gothic.VR.Interaction.Components
{
    /// <summary>
    /// Transparent copy of a block hand that shows the real controller pose while the physical hand is blocked
    /// (wall, table, chest). Created at runtime by VrPhysicsHand: it sits where the visual hand would be without
    /// physics and copies the block poses (finger and thumb curl) of the visual hand every frame.
    /// </summary>
    public class VrGhostHand : MonoBehaviour
    {
        private readonly List<(Transform Source, Transform Copy)> _blocks = new();
        private Transform _sourceRoot;

        /// <summary>
        /// Builds a ghost of all renderers below sourceRoot as a sibling of it (same parent, same local rest pose).
        /// </summary>
        public static VrGhostHand Create(Transform sourceRoot, Vector3 restPosition, Quaternion restRotation,
            Material material)
        {
            var go = new GameObject($"{sourceRoot.name} (Ghost)")
            {
                layer = sourceRoot.gameObject.layer
            };
            go.transform.SetParent(sourceRoot.parent, false);
            go.transform.SetLocalPositionAndRotation(restPosition, restRotation);
            go.transform.localScale = sourceRoot.localScale;

            var ghost = go.AddComponent<VrGhostHand>();
            ghost.Build(sourceRoot, material);
            ghost.SetVisible(false);
            return ghost;
        }

        private void Build(Transform sourceRoot, Material material)
        {
            _sourceRoot = sourceRoot;

            foreach (var sourceRenderer in sourceRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null)
                    continue;

                var copy = new GameObject(sourceRenderer.name)
                {
                    layer = gameObject.layer
                };
                copy.transform.SetParent(transform, false);
                copy.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;

                var copyRenderer = copy.AddComponent<MeshRenderer>();
                copyRenderer.sharedMaterial = material;
                copyRenderer.shadowCastingMode = ShadowCastingMode.Off;
                copyRenderer.receiveShadows = false;

                _blocks.Add((sourceRenderer.transform, copy.transform));
            }

            CopyBlockPoses();
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf != isVisible)
                gameObject.SetActive(isVisible);
        }

        private void LateUpdate()
        {
            CopyBlockPoses();
        }

        /// <summary>
        /// Same pose relative to the ghost root as the source block has relative to the visual root.
        /// </summary>
        private void CopyBlockPoses()
        {
            if (_sourceRoot == null)
                return;

            var inverseRootRotation = Quaternion.Inverse(_sourceRoot.rotation);
            var rootScale = _sourceRoot.lossyScale;
            foreach (var (source, copy) in _blocks)
            {
                if (source == null)
                    continue;

                var localPosition = _sourceRoot.InverseTransformPoint(source.position);
                var localRotation = inverseRootRotation * source.rotation;
                copy.SetLocalPositionAndRotation(localPosition, localRotation);
                copy.localScale = Divide(source.lossyScale, rootScale);
            }
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor)
        {
            return new Vector3(value.x / divisor.x, value.y / divisor.y, value.z / divisor.z);
        }
    }
}
