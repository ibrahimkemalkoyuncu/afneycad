// NE: Temel Vertex/Pixel Shader (Basic.hlsl)
// NEDEN: Sifirdan D3D11 motorunun ilk render pipeline'i - World-View-Projection donusumu
//        + tek yonlu (Lambertian) isik. Afney.Cad.Render3D.Renderer tarafindan derlenip
//        yuklenir (Vortice.D3DCompiler).

cbuffer TransformBuffer : register(b0)
{
    float4x4 WorldViewProjection;
    float4x4 World;
    float3 LightDirection;
    float _padding;
    float4 BaseColor;
};

struct VS_INPUT
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float3 Normal   : NORMAL;
};

PS_INPUT VSMain(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    output.Normal = normalize(mul(float4(input.Normal, 0.0f), World).xyz);
    return output;
}

float4 PSMain(PS_INPUT input) : SV_TARGET
{
    float3 n = normalize(input.Normal);
    float diffuse = saturate(dot(n, -normalize(LightDirection)));
    float ambient = 0.35f;
    float lighting = saturate(ambient + diffuse * 0.65f);
    float3 linearColor = BaseColor.rgb * lighting;

    // Gamma-dogru render: aydinlatma lineer uzayda hesaplanir, cikista gamma-encode edilir
    // (endustri standardi - aksi halde orta tonlar gercekte oldugundan daha koyu/kontrastsiz gorunur).
    float3 gammaEncoded = pow(saturate(linearColor), 1.0f / 2.2f);
    return float4(gammaEncoded, BaseColor.a);
}
