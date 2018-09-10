using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Seiro.ClothSimulation.GPU
{

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClothPoint
    {
        public Vector3 position;
        public Vector3 prevPosition;
        public float weight;
    }

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClothConstraint
    {
        public int aIdx;
        public int bIdx;
        public float len;
        public Vector3 typeWeight;

        public ClothConstraint(int aIdx, int bIdx, float len, int type)
        {
            this.aIdx = aIdx;
            this.bIdx = bIdx;
            this.len = len;
            this.typeWeight = new Vector3();
            if (type == 0) this.typeWeight.x = 1f;
            else if (type == 1) this.typeWeight.y = 1f;
            else if (type == 2) this.typeWeight.z = 1f;
        }
    }

    [System.Serializable]
    public class Cloth
    {
        public ClothPoint[] points;
        public ClothConstraint[] constraints;

        public static Cloth Create(Vector2 scale, int div)
        {
            var cloth = new Cloth();
            cloth.points = InitializeClothPoints(scale, div);
            cloth.constraints = InitializeClothConstraints(cloth.points, div);
            return cloth;
        }

        /// <summary>
        /// スケールと分割数を設定して、布上の質点を作成する
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="div"></param>
        static ClothPoint[] InitializeClothPoints(Vector2 scale, int div)
        {
            int divPlusOne = div + 1;
            var points = new ClothPoint[divPlusOne * divPlusOne];
            for (int x = 0; x < divPlusOne; ++x)
            {
                for (int y = 0; y < divPlusOne; ++y)
                {
                    var pos = new Vector3(scale.x / (float)div * x, -(scale.y / (float)div * y), 0f);
                    var p = new ClothPoint()
                    {
                        position = pos,
                        prevPosition = pos,
                        // 一番上の辺は固定点
                        weight = y == 0 && x % div == 0 ? 0f : 1f,
                        // weight = 1,
                    };
                    points[x * divPlusOne + y] = p;
                }
            }
            return points;
        }

        /// <summary>
        /// 指定の布上の質点に応じてバネ制約を作成する
        /// </summary>
        /// <param name="xMax"></param>
        /// <param name="yMax"></param>
        /// <returns></returns>
        static ClothConstraint[] InitializeClothConstraints(ClothPoint[] src, int div)
        {
            int w = div + 1;
            int h = div + 1;
            var constraints = new List<ClothConstraint>();
            ClothConstraint c;

            for (int x = 0; x < w; ++x)
            {
                for (int y = 0; y < h; ++y)
                {
                    // Structual spring
                    if (TryGenConstraint(x, y, -1, 0, 0, w, h, src, out c)) constraints.Add(c);
                    if (TryGenConstraint(x, y, 0, -1, 0, w, h, src, out c)) constraints.Add(c);

                    // Shear springs
                    if (TryGenConstraint(x, y, -1, -1, 1, w, h, src, out c)) constraints.Add(c);
                    if (TryGenConstraint(x, y, 1, -1, 1, w, h, src, out c)) constraints.Add(c);

                    // Bending springs
                    if (TryGenConstraint(x, y, -2, 0, 2, w, h, src, out c)) constraints.Add(c);
                    if (TryGenConstraint(x, y, 0, -2, 2, w, h, src, out c)) constraints.Add(c);
                }
            }

            return constraints.ToArray();
        }

        /// <summary>
        /// 布の境界を考慮しつつバネ制約を生成する
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="type"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        static bool TryGenConstraint(int x1, int y, int offsetX, int offsetY, int type, int w, int h, ClothPoint[] points, out ClothConstraint dst)
        {
            var ox = x1 + offsetX;
            var oy = y + offsetY;

            if (
                (0 <= x1 && x1 < w) && (0 <= y && y < h) && (0 <= ox && ox < w) && (0 <= oy && oy < h)
            )
            {
                int aIdx = x1 * w + y;
                int bIdx = ox * w + oy;
                var len = (points[aIdx].position - points[bIdx].position).magnitude;
                dst = new ClothConstraint(aIdx, bIdx, len, type);

                // パーティクルの関与してる制約の数を増やす
                // points[aIdx].constraintCount++;
                // points[bIdx].constraintCount++;

                return true;
            }
            else
            {
                dst = default(ClothConstraint);
                return false;
            }
        }
    }


    public class GPUClothSimulator : MonoBehaviour
    {
        public Vector2 scale;
        public int div = 32;
        public ComputeShader cs = null;

        [Space]
        [Range(0.01f, 0.1f)]
        public float stepSize = 0.016f;
        float stepSize2 { get { return stepSize * stepSize; } }

        [Space]
        public Vector3 gravity = new Vector3(0f, -9.8f, 0f);
        public Vector3 wind = new Vector3(0f, 0f, 1f);
        [Range(0f, 10f)]
        public float regist = 0.2f;

        [Space]
        [Range(1, 10)]
        public int iteration = 2;
        [Range(0f, 5000f)]
        public float springConstant = 3000f;
        [Range(0f, 1f)]
        public float structualShrink = 1f;
        [Range(0f, 1f)]
        public float structualStretch = 1f;
        [Range(0f, 1f)]
        public float shearShrink = 1f;
        [Range(0f, 1f)]
        public float shearStretch = 1f;
        [Range(0f, 1f)]
        public float bendingShrink = 1f;
        [Range(0f, 1f)]
        public float bendingStretch = 1f;

        bool _isReady = false;
        public bool isReady { get { return _isReady; } }

        float _elapsedTime = 0f;

        Kernel _integrateKernel = null;
        Kernel _calcConstraintKernel = null;
        Kernel _applyConstraintKernel = null;

        ComputeBuffer _pointBuffer = null;
        public ComputeBuffer pointBuffer { get { return _pointBuffer; } }

        ComputeBuffer _constraintBuffer = null;
        public ComputeBuffer constraintBuffer { get { return _constraintBuffer; } }

        ComputeBuffer _adjusterBuffer = null;
        ComputeBuffer _weightBuffer = null;

        int _maxPointNum = 0;
        public int maxPointNum { get { return _maxPointNum; } }

        int _maxConstraintNum = 0;
        public int maxConstraintNum { get { return _maxConstraintNum; } }

        int _pointThreadGroupSize = 0;
        int _constraintThreadGroupSize = 0;

        // Cloth _cloth = null;

        #region MonoBehaviour events

        void Awake()
        {
            InitializeCloth(scale, div);
        }

        void Update()
        {
            if (_isReady)
            {
                UpdateCloth();
            }
        }

        void OnDestroy()
        {
            ReleaseBuffer();
        }

        #endregion

        #region Private functions

        /// <summary>
        /// 布の初期化
        /// </summary>
        void InitializeCloth(Vector2 scale, int div)
        {
            var cloth = Cloth.Create(scale, div);
            // _cloth = cloth;
            var result = BindBuffer(cloth.points, cloth.constraints);
            result &= SetUpKernel();

            if (!result)
            {
                Debug.LogWarning("初期化に失敗しました");
            }

            _isReady = result;
        }

        /// <summary>
        /// バッファの確保
        /// </summary>
        bool BindBuffer(ClothPoint[] points, ClothConstraint[] constraints)
        {
            if (points == null || points.Length == 0 || constraints == null || constraints.Length == 0)
            {
                return false;
            }

            ReleaseBuffer();

            var stride = 0;

            _maxPointNum = points.Length;
            stride = Marshal.SizeOf(typeof(ClothPoint));
            _pointBuffer = new ComputeBuffer(_maxPointNum, stride);
            _pointBuffer.SetData(points);

            _maxConstraintNum = constraints.Length;
            stride = Marshal.SizeOf(typeof(ClothConstraint));
            _constraintBuffer = new ComputeBuffer(_maxConstraintNum, stride);
            _constraintBuffer.SetData(constraints);

            stride = Marshal.SizeOf(typeof(Vector3));
            _adjusterBuffer = new ComputeBuffer(_maxPointNum, stride);
            var adjusterArr = new Vector3[_maxPointNum];
            for (int i = 0; i < _maxPointNum; ++i) adjusterArr[i] = new Vector3(0f, 0f, 0f);
            _adjusterBuffer.SetData(adjusterArr);
            adjusterArr = null;

            stride = sizeof(float);
            _weightBuffer = new ComputeBuffer(_maxPointNum, stride);
            var weightArr = new float[_maxPointNum];
            for (int i = 0; i < _maxPointNum; ++i) weightArr[i] = 0f;
            _weightBuffer.SetData(weightArr);
            weightArr = null;

            return true;
        }

        /// <summary>
        /// バッファの解放
        /// </summary>
        void ReleaseBuffer()
        {
            if (_pointBuffer != null)
            {
                _pointBuffer.Release();
                _pointBuffer = null;
            }
            if (_adjusterBuffer != null)
            {
                _adjusterBuffer.Release();
                _adjusterBuffer = null;
            }
            if (_constraintBuffer != null)
            {
                _constraintBuffer.Release();
                _constraintBuffer = null;
            }
            if (_weightBuffer != null)
            {
                _weightBuffer.Release();
                _weightBuffer = null;
            }
        }

        /// <summary>
        /// カーネルのセットアップ
        /// </summary>
        bool SetUpKernel()
        {
            var result =
                Kernel.TryGetKernel(cs, "Integrate", out _integrateKernel) &&
                Kernel.TryGetKernel(cs, "CalcConstraint", out _calcConstraintKernel) &&
                Kernel.TryGetKernel(cs, "ApplyConstraint", out _applyConstraintKernel);

            if (result)
            {
                _pointThreadGroupSize = Mathf.CeilToInt(_maxPointNum / (float)_integrateKernel.threadX);
                _constraintThreadGroupSize = Mathf.CeilToInt(_maxConstraintNum / (float)_calcConstraintKernel.threadX);
            }

            return result;
        }

        /// <summary>
        /// 布の更新
        /// </summary>
        void UpdateCloth()
        {
            _elapsedTime += Time.deltaTime;
            var iteration = Mathf.FloorToInt(_elapsedTime / stepSize);
            _elapsedTime -= iteration * stepSize;
            for (int i = 0; i < iteration; ++i)
            {
                Simulate();
            }
        }

        /// <summary>
        /// シミュレートする
        /// </summary>
        void Simulate()
        {
            IntegrateStep();
            for (int i = 0; i < iteration; ++i)
            {
                RelaxationStep();
            }
        }

        /// <summary>
        /// 積分ステップ
        /// </summary>
        void IntegrateStep()
        {
            // integrate
            var d = gravity;
            d += wind * (Mathf.Sin(Time.time) * 0.5f + 0.5f);
            d *= (stepSize2 * 0.5f);
            var r = Mathf.Max(1f - regist * stepSize, 0f);

            cs.SetBuffer(_integrateKernel, "pointBuffer", _pointBuffer);
            cs.SetFloat("dt", stepSize);
            cs.SetInt("maxPointNum", _maxPointNum);
            cs.SetVector("disturbance", d);
            cs.SetFloat("regist", r);
            cs.Dispatch(_integrateKernel, _pointThreadGroupSize, 1, 1);
        }

        /// <summary>
        /// バネの更新ステップ
        /// </summary>
        void RelaxationStep()
        {
            // calc constraint
            cs.SetBuffer(_calcConstraintKernel, "constraintBuffer", _constraintBuffer);
            cs.SetBuffer(_calcConstraintKernel, "pointBuffer", _pointBuffer);
            cs.SetBuffer(_calcConstraintKernel, "adjusterBuffer", _adjusterBuffer);
            cs.SetBuffer(_calcConstraintKernel, "weightBuffer", _weightBuffer);
            cs.SetFloat("dt", stepSize);
            cs.SetInt("maxConstraintNum", _maxConstraintNum);
            cs.SetFloat("springConstant", springConstant);
            cs.SetVector("shrink", new Vector3(structualShrink, shearShrink, bendingShrink));
            cs.SetVector("stretch", new Vector3(structualStretch, shearStretch, bendingStretch));
            cs.Dispatch(_calcConstraintKernel, _constraintThreadGroupSize, 1, 1);

            // apply constraint
            cs.SetBuffer(_applyConstraintKernel, "pointBuffer", _pointBuffer);
            cs.SetBuffer(_applyConstraintKernel, "adjusterBuffer", _adjusterBuffer);
            cs.SetBuffer(_applyConstraintKernel, "weightBuffer", _weightBuffer);
            cs.SetInt("maxPointNum", _maxPointNum);
            cs.Dispatch(_applyConstraintKernel, _constraintThreadGroupSize, 1, 1);
        }

        #endregion
    }
}