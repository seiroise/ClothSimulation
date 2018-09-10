using UnityEngine;

namespace Seiro.ClothSimulation.GPU
{
    public class GPUClothRenderer : MonoBehaviour
    {
        public Mesh pointMesh = null;
        public Material pointMaterial = null;
        public Vector3 pointScale = new Vector3(0.01f, 0.01f, 0.01f);

        [Space]
        public Mesh constraintMesh = null;
        public Material constraintMaterial = null;
        public float constraintWidth = 0.01f;

        public Bounds bounds = new Bounds(Vector3.zero, new Vector3(32f, 32f, 32f));

        bool _isReady = false;

        GPUClothSimulator _simulator = null;
        uint[] _args = { 0, 0, 0, 0, 0 };
        ComputeBuffer _pointArgsBuffer = null;
        ComputeBuffer _constraintArgsBuffer = null;

        #region MonoBehaviour events

        void Start()
        {
            _isReady = PrepareRendering();
        }

        void Update()
        {
            Render();
        }

        void OnDestroy()
        {
            ReleaseBuffer();
        }

        #endregion

        #region Private functions

        /// <summary>
        /// レンダリングするための準備をする
        /// </summary>
        /// <returns></returns>
        bool PrepareRendering()
        {
            if (!pointMesh || !pointMaterial || !constraintMesh || !constraintMaterial)
            {
                return false;
            }

            _simulator = GetComponent<GPUClothSimulator>();
            if (!_simulator) return false;

            return BindBuffer();
        }

        /// <summary>
        /// バッファの確保
        /// </summary>
        /// <returns></returns>
        bool BindBuffer()
        {
            ReleaseBuffer();
            _pointArgsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _constraintArgsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            return true;
        }

        /// <summary>
        /// バッファの解放
        /// </summary>
        void ReleaseBuffer()
        {
            if (_pointArgsBuffer != null)
            {
                _pointArgsBuffer.Release();
                _pointArgsBuffer = null;
            }
            if (_constraintArgsBuffer != null)
            {
                _constraintArgsBuffer.Release();
                _constraintArgsBuffer = null;
            }
        }


        /// <summary>
        /// 描画する
        /// </summary>
        void Render()
        {
            if (!_isReady) return;
            if (!_simulator.isReady) return;

            RenderPoints();
            RenderConstraints();
        }

        /// <summary>
        /// 質点の描画
        /// </summary>
        void RenderPoints()
        {
            _args[0] = pointMesh.GetIndexCount(0);
            _args[1] = (uint)_simulator.maxPointNum;
            _pointArgsBuffer.SetData(_args);

            pointMaterial.SetBuffer("_PointBuffer", _simulator.pointBuffer);
            pointMaterial.SetVector("_ObjectScale", pointScale);
            Graphics.DrawMeshInstancedIndirect(pointMesh, 0, pointMaterial, bounds, _pointArgsBuffer);
        }

        /// <summary>
        /// スプリングのレンダリング
        /// </summary>
        void RenderConstraints()
        {
            _args[0] = constraintMesh.GetIndexCount(0);
            _args[1] = (uint)_simulator.maxConstraintNum;
            _constraintArgsBuffer.SetData(_args);

            constraintMaterial.SetBuffer("_ConstraintBuffer", _simulator.constraintBuffer);
            constraintMaterial.SetBuffer("_PointBuffer", _simulator.pointBuffer);
            constraintMaterial.SetFloat("_Width", constraintWidth);
            Graphics.DrawMeshInstancedIndirect(constraintMesh, 0, constraintMaterial, bounds, _constraintArgsBuffer);
        }

        #endregion
    }
}