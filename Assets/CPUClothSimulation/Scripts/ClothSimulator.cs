using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Seiro.ClothSimulation
{

	/// <summary>
	/// 布上の質点
	/// </summary>
	[Serializable]
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
		public Vector3 gravity = new Vector3(0f, -9.8f, 0f);
		public Vector3 wind = new Vector3(0f, 0f, 1f);
		[Range(0f, 10f)]
		public float regist = 0.2f;
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
		public float bendingStretch = 0.5f;

		ClothPoint[] _clothPoints;
		ClothConstraint[] _clothConstraints;

		#region MonoBehaviour events

		void Awake()
		{
			InitializeCloth();
		}

		void Update()
		{
			UpdatePoints();
		}

		#endregion

		#region Initialization functions

		void InitializeCloth()
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
					var pos = new Vector3(scale.x / (float)div * x, -(scale.y / (float)div * y), 0f);
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

		/// <summary>
		/// 質点の位置を更新する
		/// </summary>
		void UpdatePoints()
		{
			var dt = Mathf.Clamp(Time.deltaTime, 0.016f, 0.1f);
			var time = Time.time;

			// ベルレ積分
			{
				var f = Vector3.zero;
				f += gravity;   // 重力
				f += wind * (Mathf.Sin(time) * 0.5f + 0.5f);    // 風力
				f *= (dt * dt * 0.5f);

				var r = Mathf.Max(1f - regist * dt, 0f);    // 抵抗

				for (int i = 0; i < _clothPoints.Length; ++i)
				{
					var t = _clothPoints[i];
					var dp = Vector3.zero;
					dp += t.position - t.prevPosition;  // 速度
					t.prevPosition = t.position;

					dp += f;        // 力の変位
					dp *= r;        // 抵抗
					dp *= t.weight; // 質点自体の抵抗

					// 位置更新
					t.position += dp;

					_clothPoints[i] = t;
				}
			}

			// 制約充足
			{
				for (int ite = 0; ite < iteration; ++ite)
				{

					for (int i = 0; i < _clothConstraints.Length; ++i)
					{
						var t = _clothConstraints[i];
						var a = _clothPoints[t.aIdx];
						var b = _clothPoints[t.bIdx];

						if (a.weight + b.weight == 0f) continue;

						var shrink = 0f;
						var stretch = 0f;

						if (t.type == 0)
						{
							shrink = structualShrink;
							stretch = structualStretch;
						}
						else if (t.type == 1)
						{
							shrink = shearShrink;
							stretch = shearStretch;
						}
						else if (t.type == 2)
						{
							shrink = bendingShrink;
							stretch = bendingStretch;
						}

						var d = (a.position - b.position).magnitude;
						float f = (d - t.len) * springConstant;
						f *= f >= 0f ? shrink : stretch;

						var dp = (b.position - a.position).normalized;
						dp *= f;
						dp *= (dt * dt * 0.5f);
						// Debug.Log(dp);

						var adx = dp * (a.weight / (a.weight + b.weight));
						a.position += adx;

						var bdx = dp * (b.weight / (a.weight + b.weight));
						b.position -= bdx;

						_clothPoints[t.aIdx] = a;
						_clothPoints[t.bIdx] = b;
					}
				}
			}
		}

		#endregion

		#region Editor

#if UNITY_EDITOR

		[Header("Editor")]

		public Vector3 drawPointSize = new Vector3(0.1f, 0.1f, 0.1f);
		public Color pointColor = Color.white;
		public Color[] constraintColor = {
			Color.white, Color.red, Color.green
		};

		void OnDrawGizmos()
		{
			if (Application.isPlaying)
			{
				DrawConstraintOnGizmos();
				DrawPointOnGizmos();				
			}
		}

		/// <summary>
		/// 質点の描画
		/// </summary>
		void DrawPointOnGizmos()
		{
			Gizmos.color = pointColor;
			for (int i = 0; i < _clothPoints.Length; ++i)
			{
				Gizmos.DrawCube(_clothPoints[i].position, drawPointSize);
			}
		}

		/// <summary>
		/// 制約の描画
		/// </summary>
		void DrawConstraintOnGizmos()
		{
			for (int i = 0; i < _clothConstraints.Length; ++i)
			{
				var t = _clothConstraints[i];
				var a = _clothPoints[t.aIdx];
				var b = _clothPoints[t.bIdx];
				Gizmos.color = constraintColor[t.type];
				Gizmos.DrawLine(a.position, b.position);
			}
		}

#endif

		#endregion
	}
}