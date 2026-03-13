using UnityEngine;
using DG.Tweening;

namespace VJSystem
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SpawnedMeshObject : MonoBehaviour
    {
        public float rotationSpeed = 20f;
        Vector3 _rotAxis;

        void Awake()
        {
            _rotAxis = Random.onUnitSphere;
        }

        void Update()
        {
            transform.Rotate(_rotAxis, rotationSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>Animate to new position (used by Scramble).</summary>
        public void MoveTo(Vector3 pos, float duration)
        {
            // position and scale are separate properties — no tween conflict
            transform.DOMove(pos, duration).SetEase(Ease.InOutQuad);
        }

        /// <summary>Scale out then destroy self.</summary>
        public void ScaleOut(float duration)
        {
            DOTween.Kill(transform);
            transform.DOScale(Vector3.zero, duration)
                .SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}
