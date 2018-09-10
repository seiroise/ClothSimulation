Shader "Hidden/ClothSimulation/Constraint" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard vertex:vert addshadow
		#pragma instancing_options procedural:setup
		#pragma target 4.5

		#include "./Cloth.cginc"
		// #include "./Quaternion.cginc"

		struct Input {
			float2 uv_MainTex;
			// float4 color : COLOR;
		};

		sampler2D _MainTex;

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		StructuredBuffer<ClothConstraint> _ConstraintBuffer;
		StructuredBuffer<ClothPoint> _PointBuffer;
		#endif

		float _Width;

		float4x4 eulerAnglesToRotationMatrix(float3 angles)
		{
			float ch = cos(angles.y); float sh = sin(angles.y);
			float ca = cos(angles.z); float sa = sin(angles.z);
			float cb = cos(angles.x); float sb = sin(angles.x);

			// yxz
			return float4x4
			(
				ch * ca + sh * sb * sa, -ch * sa + sh * sb * ca, sh * cb, 0,
				cb * sa, cb * ca, -sb, 0,
				-sh * ca + ch * sb * sa, sh * sa + ch * sb * ca, ch * cb, 0,
				 0, 0, 0, 1
			);
		}

		float4x4 makeRotationDir(float3 direction, float3 up)
		{
			float3 xaxis = normalize(cross(up, direction));
			float3 yaxis = normalize(cross(direction, xaxis));

			return float4x4
			(
				xaxis.x, yaxis.x, direction.x, 0,
				xaxis.y, yaxis.y, direction.y, 0,
				xaxis.z, yaxis.z, direction.z, 0,
				0, 0, 0, 1
			);
		}

		void setup()
		{
		}

		void vert(inout appdata_full v)
		{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

			ClothConstraint c = _ConstraintBuffer[unity_InstanceID];

			ClothPoint pa = _PointBuffer[c.aIdx];
			ClothPoint pb = _PointBuffer[c.bIdx];

			float4x4 objectToWorld = (float4x4)0;

			float3 diff = pb.position - pa.position;
			float len = sqrt(dot(diff, diff));
			objectToWorld._11_22_33_44 = float4(_Width, _Width, len, 1);

			float3 dir = normalize(diff);
			float4x4 rotMat = makeRotationDir(dir, float3(0, 1, 0));

			objectToWorld =mul(rotMat, objectToWorld);
			objectToWorld._14_24_34 += (pa.position + pb.position) * 0.5;

			v.vertex = mul(objectToWorld, v.vertex);
			v.normal = normalize(mul(objectToWorld, v.normal));

			#endif
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
