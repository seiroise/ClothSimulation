Shader "Hidden/ClothSimulation/Point" {
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

		struct Input {
			float2 uv_MainTex;
		};

		sampler2D _MainTex;

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		StructuredBuffer<ClothPoint> _PointBuffer;
		#endif

		float3 _ObjectScale;

		void setup()
		{
		}

		void vert(inout appdata_full v)
		{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

			ClothPoint p = _PointBuffer[unity_InstanceID];

			float4x4 objectToWorld = (float4x4)0;
			objectToWorld._11_22_33_44 = float4(_ObjectScale, 1);

			objectToWorld._14_24_34 += p.position;

			v.vertex = mul(objectToWorld, v.vertex);
			v.normal = normalize(mul(objectToWorld, v.normal));
			#endif
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb; // * IN.color;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
