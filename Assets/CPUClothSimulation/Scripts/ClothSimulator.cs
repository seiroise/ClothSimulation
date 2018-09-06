using System.Collections.Generic;
using UnityEngine;

namespace Seiro.ClothSimulation
{

    /// <summary>
    /// 布上の質点
    /// </summary>
    public struct ClothPoint
    {
        public Vector3 position;
        public Vector3 prevPosition;
        public float weight;
    }

    /// <summary>
    /// 質点感のバネ制約
    /// </summary>
    public struct ClothConstraint
    {
        public int aIdx;
        public int bIdx;
        public float len;
        public int type;

        public ClothConstraint(int aIdx, int bIdx, float len, int type)
        {
            this.aIdx = aIdx;
            this.bIdx = bIdx;
            this.len = len;
            this.type = type;
        }
    }

    public class ClothSimulator : MonoBehaviour
    {

        public Vector2 scale;
        public int div = 32;

        ClothPoint[] _clothPoints;
        ClothConstraint[] _clothConstraints;

        #region MonoBehaviour events

        void Awake()
        {

        }

        void Update()
        {

        }

        #endregion

        #region Initialization functions

        void Initialize()
        {
            _clothPoints = InitializeClothPoints(scale, div);
            _clothConstraints = InitializeClothConstraints(_clothPoints, div);
        }

        /// <summary>
        /// スケールと分割数を設定して、布上の質点を作成する
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="div"></param>
        ClothPoint[] InitializeClothPoints(Vector2 scale, int div)
        {
            int divPlusOne = div + 1;
            var points = new ClothPoint[divPlusOne * divPlusOne];
            for (int x = 0; x < divPlusOne; ++x)
            {
                for (int y = 0; y < divPlusOne; ++y)
                {
                    var pos = new Vector3(scale.x / (float)div * x, scale.y / (float)div, 0f);
                    var p = new ClothPoint()
                    {
                        position = pos,
                        prevPosition = pos,
                        weight = y == 0 ? 0f : 1f,
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
        ClothConstraint[] InitializeClothConstraints(ClothPoint[] src, int div)
        {
            int w = div;
            int h = div;
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
                    if (TryGenConstraint(x, y, 1, 1, 1, w, h, src, out c)) constraints.Add(c);

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
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="type"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        bool TryGenConstraint(int x, int y, int offsetX, int offsetY, int type, int w, int h, ClothPoint[] points, out ClothConstraint dst)
        {
            var ox = x + offsetX;
            var oy = y + offsetY;

            if (
                (0 <= x && x < w) && (0 <= y && y < h) && (0 <= ox && ox < w) && (0 <= oy && oy < h)
            )
            {
                int aIdx = x * w + y;
                int bIdx = ox * w + oy;
                var len = (points[aIdx].position - points[bIdx].position).magnitude;
                dst = new ClothConstraint(aIdx, bIdx, len, type);
                return true;
            }
            else
            {
                dst = default(ClothConstraint);
                return false;
            }
        }

        #endregion

        #region Update functions



        #endregion
    }
}