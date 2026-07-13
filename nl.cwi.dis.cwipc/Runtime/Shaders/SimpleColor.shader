//
// A trivial flat, unlit shader (optionally textured, multiplied by _Color), used for
// placeholder/backdrop geometry in the samples (the demo floor, avatar body-part placeholders,
// the PM5644 test-pattern plane) that has no need for the full Standard/Lit shader - in
// particular it does not respond to scene lighting at all. Ships as a BIRP/URP subshader pair,
// like PointCloudTextured.shader, so the same material works unchanged under both pipelines.
//
Shader "cwipc/SimpleColor"{
	Properties {
		_Color("Color", Color) = (0.8, 0.8, 0.8, 1)
		_MainTex("Texture", 2D) = "white" {}
	}

	SubShader {
		// URP version.
		Tags {
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
		}
		LOD 100

		Pass {
			Tags {
				"LightMode" = "SRPDefaultUnlit"
			}
			HLSLPROGRAM

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			half4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings {
				float4 positionCS : SV_Position;
				float2 uv : TEXCOORD0;
			};

			Varyings Vertex(Attributes v) {
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
				return o;
			}

			half4 Fragment(Varyings i) : SV_Target{
				return tex2D(_MainTex, i.uv) * _Color;
			}

			ENDHLSL
		}
	}

	SubShader {
		// Built-in pipeline version.
		Tags {
			"RenderType" = "Opaque"
		}
		LOD 100

		Pass {
			CGPROGRAM

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "UnityCG.cginc"

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings {
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vertex(Attributes v) {
				Varyings o;
				o.positionCS = UnityObjectToClipPos(v.positionOS);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			fixed4 Fragment(Varyings i) : SV_Target{
				return tex2D(_MainTex, i.uv) * _Color;
			}

			ENDCG
		}
	}
}
