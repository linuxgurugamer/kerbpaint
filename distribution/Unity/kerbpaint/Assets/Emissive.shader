﻿Shader "KerbPaint/Emissive/Diffuse"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}

		_EmissiveColor("_EmissiveColor", Color) = (0,0,0,1)
		_Emissive("_Emissive", 2D) = "white" {}
		
		_Opacity("_Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColor("_RimColor", Color) = (0,0,0,0)
		
		_Mask("Mask Texture",2D) = "black" {}
		_AWght("Primary Channel",Vector) = (0,0,1,0)
		_BWght("Secondary Channel",Vector) = (0,0,1,0)
		_CWght("Tertiary Channel",Vector) = (0,0,1,0)
	}
	
	SubShader 
	{
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM
		#include "KerbPaint.cginc"
		#pragma only_renderers opengl d3d9
		#pragma surface surf Lambert
		#pragma target 3.0

		sampler2D _MainTex;

		float4 _EmissiveColor;
		sampler2D _Emissive;

		float _Opacity;
		float _RimFalloff;
		float4 _RimColor;
		
		struct Input
		{
			float2 uv_MainTex;
			float2 uv_Emissive;
			float3 viewDir;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex));
			float3 normal = float3(0,0,1);

			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

			float3 emission = (_RimColor.rgb * pow(rim, _RimFalloff)) * _RimColor.a;
			emission += (tex2D(_Emissive, IN.uv_Emissive).rgb * _EmissiveColor.rgb) * _EmissiveColor.a;

			o.Albedo = ApplyWeights(color.rgb,tex2D(_Mask,IN.uv_MainTex).rgb);;
			o.Emission = emission;
			o.Gloss = 0;
			o.Specular = 0;
			o.Normal = normal;

			o.Emission *= _Opacity;
			o.Alpha = _Opacity;
		}
		ENDCG
	}
	Fallback "KSP/Emissive/Diffuse"
}