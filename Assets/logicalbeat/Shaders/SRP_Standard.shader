Shader "SRP/Standard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        //
        // <comment>
        // Renderer側の「Receive Shadows」が活用できないので、マテリアル側でパラメータを持つ
        // LWRPもそうなっていたが、更にRenderer側の項目を消していて分かりやすくなっている
        // どうやって実現しているのだろうか
        //
        [Toggle(SHADOW_RECEIVE)]_ShadowReceive ("Receive Shadows", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        //
        // <comment>
        // 通常描画部分です
        // ビルトインシェーダのUnlitからの改造です
        //
        Pass
        {
            Tags { "LightMode"="lbForward"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile _ SHADOW_RECEIVE

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 wpos : TEXCOORD2;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _ShadowMap;
            float4x4  _ShadowLightVP;
//          UNITY_DECLARE_TEXCUBE(unity_SpecCube0);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.wpos = mul( unity_ObjectToWorld, v.vertex );
                o.normal = mul( (float3x3)UNITY_MATRIX_M, v.normal );
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);

                //
                // <comment>
                // 環境マップを適応
                // 講演でお話ししたunity_SpecCube0を参照しています
                //
                col.rgb = lerp( col.rgb, UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,i.normal,0).rgb, 0.5f );

#if	SHADOW_RECEIVE
                // 投影テクスチャシャドウを適応
                float4	shadowProj = mul( _ShadowLightVP, i.wpos );
                shadowProj = UNITY_PROJ_COORD(ComputeScreenPos(shadowProj));
                float3	coord = shadowProj.xyz / shadowProj.www;
                col.rgb *= lerp( 0.5f, 1.0f, tex2D( _ShadowMap, coord.xy ).r );		// 少し薄くする
#endif

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        //
        // <comment>
        // シャドウマップを作る用のシェーダです
        // デプスバッファを用いず、カラー減算で描画しています
        // (投影シャドウなので影の有無だけ描ければOK)
        //
        Pass
        {
            Name "SHADOWCASTER"

            Tags { "LightMode"="ShadowCaster" }

            BlendOp RevSub
            Blend One One
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct VS_INPUT
            {
                float4  vertex : POSITION;
                half3   normal : NORMAL;
                half2   uv     : TEXCOORD0;
            };

            struct VS_OUTPUT
            {
                float4  vertex : SV_POSITION;
                half2   uv     : TEXCOORD0;
            };

            struct PS_INPUT
            {
                UNITY_VPOS_TYPE vertex : VPOS;
                half2           uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4x4  _ShadowLightVP;

            VS_OUTPUT vert (VS_INPUT v)
            {
                // 雑用変数の宣言
                VS_OUTPUT   o;

                // 座標算出
                float4  wpos    = mul(unity_ObjectToWorld, v.vertex);
                o.vertex = mul( _ShadowLightVP/*UNITY_MATRIX_VP*/, wpos );

                // UV設定
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return  ( o );
            }

            float4 frag (VS_OUTPUT i) : SV_Target
            {
                half4   albedo = tex2D( _MainTex, i.uv );
                return  ( albedo.aaaa );
            }
            ENDCG
        }
    }
}
