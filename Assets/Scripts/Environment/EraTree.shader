Shader "EraImperial/Environment/EraTree"
{
    Properties
    {
        _MainTex ("Fallback atlas", 2D) = "white" {}
        _EITreeAncientTex ("Ancient atlas", 2D) = "white" {}
        _EITreeClassicalTex ("Classical atlas", 2D) = "white" {}
        _EITreeMedievalTex ("Medieval atlas", 2D) = "white" {}
        _EITreeImperialTex ("Imperial atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 180
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EITreeAncientTex;
        sampler2D _EITreeClassicalTex;
        sampler2D _EITreeMedievalTex;
        sampler2D _EITreeImperialTex;
        float _EITreeEra;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            // One era value is shared by all trees; no per-instance data or CPU updates.
            fixed4 atlas;
            if (_EITreeEra < 0.5)
                atlas = tex2D(_EITreeAncientTex, IN.uv_MainTex);
            else if (_EITreeEra < 1.5)
                atlas = tex2D(_EITreeClassicalTex, IN.uv_MainTex);
            else if (_EITreeEra < 2.5)
                atlas = tex2D(_EITreeMedievalTex, IN.uv_MainTex);
            else
                atlas = tex2D(_EITreeImperialTex, IN.uv_MainTex);

            fixed3 color = atlas.rgb;
            o.Albedo = color;
            // A modest floor keeps the canopy readable under the maps' dark ambient light.
            o.Emission = color * 0.14;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
