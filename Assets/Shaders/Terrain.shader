Shader"Custom/Terrain"
{
    Properties
    {
        _GrassColor ("Color", Color) = (0,1,0,1)
        _RockColor ("Color", Color) = (1,1,1,1)
        _GrassSlopeThreshold ("Grass Slope Treshold", Range(0,1)) = 0.5
        _GrassBlendAmount ("Grass Blend Amount", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

struct Input
{
    float3 worldPos;
    float3 worldNormal;
};

half _MaxHeight;
half _GrassSlopeThreshold;
half _GrassBlendAmount;
fixed4 _GrassColor;
fixed4 _RockColor;


void surf(Input IN, inout SurfaceOutputStandard o)
{
    float slope = 1 - IN.worldNormal.y;
    float grassBlendHeight = _GrassSlopeThreshold * (1 - _GrassBlendAmount);
    float grassWeight = 1 - saturate((slope - grassBlendHeight) / (_GrassSlopeThreshold - grassBlendHeight));
    o.Albedo = _GrassColor * grassWeight + _RockColor * (1 - grassWeight);
}
        ENDCG
    }
}