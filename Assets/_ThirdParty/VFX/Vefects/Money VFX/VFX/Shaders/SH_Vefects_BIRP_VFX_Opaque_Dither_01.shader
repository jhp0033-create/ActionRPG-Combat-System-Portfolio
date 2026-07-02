// Made with Amplify Shader Editor v1.9.9.9
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_VFX_Opaque_Dither_01"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		[Space(33)][Header(Dither)][Space(13)] _DitherOpacity( "Dither Opacity", Range( 0, 1 ) ) = 1
		[Space(33)][Header(Camera Fade)][Space(13)] _CameraFadeLength( "Camera Fade Length", Float ) = 0.1
		_CameraFadeOffset( "Camera Fade Offset", Float ) = 0
		_CameraFadeFarLength( "Camera Fade Far Length", Float ) = 10
		_CameraFadeFarOffset( "Camera Fade Far Offset", Float ) = 20
		[Space(33)][Header(Main Texture)][Space(13)] _MainTexture( "Main Texture", 2D ) = "white" {}
		_Color( "Color", Color ) = ( 1, 1, 1, 0 )
		_FlatColor( "Flat Color", Float ) = 0
		_SmoothnessMin( "Smoothness Min", Float ) = 0
		_SmoothnessMax( "Smoothness Max", Float ) = 1
		_Metallic( "Metallic", Float ) = 1
		_Normal( "Normal", 2D ) = "white" {}
		_NormalIntensity( "Normal Intensity", Float ) = 1
		_Emission( "Emission", Float ) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19909
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows exclude_path:deferred vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 screenPosition;
			float eyeDepth;
		};

		uniform sampler2D _Normal;
		uniform float4 _Normal_ST;
		uniform float _NormalIntensity;
		uniform float4 _Color;
		uniform sampler2D _MainTexture;
		uniform float4 _MainTexture_ST;
		uniform float _FlatColor;
		uniform float _Emission;
		uniform float _Metallic;
		uniform float _SmoothnessMax;
		uniform float _SmoothnessMin;
		uniform float _DitherOpacity;
		uniform float _CameraFadeLength;
		uniform float _CameraFadeOffset;
		uniform float _CameraFadeFarLength;
		uniform float _CameraFadeFarOffset;
		uniform float _Cutoff = 0.5;


		inline float Dither8x8Bayer( uint x, uint y )
		{
			const float dither[ 64 ] = {
			     1, 49, 13, 61,  4, 52, 16, 64,
			    33, 17, 45, 29, 36, 20, 48, 32,
			     9, 57,  5, 53, 12, 60,  8, 56,
			    41, 25, 37, 21, 44, 28, 40, 24,
			     3, 51, 15, 63,  2, 50, 14, 62,
			    35, 19, 47, 31, 34, 18, 46, 30,
			    11, 59,  7, 55, 10, 58,  6, 54,
			    43, 27, 39, 23, 42, 26, 38, 22};
			uint r = y * 8 + x;
			return dither[ min( r, 63 ) ] / 64; // same # of instructions as pre-dividing due to compiler magic
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 ase_positionSS = ComputeScreenPos( UnityObjectToClipPos( v.vertex ) );
			o.screenPosition = ase_positionSS;
			o.eyeDepth = -UnityObjectToViewPos( v.vertex.xyz ).z;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			float3 lerpResult14 = lerp( float3( 0, 0, 1 ) , tex2D( _Normal, uv_Normal ).rgb , _NormalIntensity);
			float3 NORM69 = lerpResult14;
			o.Normal = NORM69;
			float2 uv_MainTexture = i.uv_texcoord * _MainTexture_ST.xy + _MainTexture_ST.zw;
			float4 tex2DNode11 = tex2D( _MainTexture, uv_MainTexture );
			float3 lerpResult25 = lerp( ( _Color.rgb * tex2DNode11.rgb ) , _Color.rgb , _FlatColor);
			float4 temp_output_27_0 = ( float4( lerpResult25 , 0.0 ) * i.vertexColor );
			float4 MCOL61 = temp_output_27_0;
			o.Albedo = MCOL61.rgb;
			float4 EM63 = ( temp_output_27_0 * _Emission );
			o.Emission = EM63.rgb;
			float MET67 = _Metallic;
			o.Metallic = MET67;
			float lerpResult19 = lerp( _SmoothnessMax , _SmoothnessMin , tex2DNode11.g);
			float SMO65 = lerpResult19;
			o.Smoothness = SMO65;
			o.Alpha = 1;
			float4 ase_positionSS = i.screenPosition;
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float4 ditherScreenPos31 = ase_positionSSNorm;
			float2 ditherScreenPosPixel31 = ditherScreenPos31.xy * _ScreenParams.xy;
			float dither31 = Dither8x8Bayer( fmod( ditherScreenPosPixel31.x, 8 ), fmod( ditherScreenPosPixel31.y, 8 ) );
			float cameraDepthFade43 = (( i.eyeDepth -_ProjectionParams.y - _CameraFadeOffset ) / _CameraFadeLength);
			float cameraDepthFade57 = (( i.eyeDepth -_ProjectionParams.y - _CameraFadeFarOffset ) / _CameraFadeFarLength);
			float CAMFADE44 = ( saturate( cameraDepthFade43 ) * saturate( ( 1.0 - saturate( cameraDepthFade57 ) ) ) );
			float VCA33 = i.vertexColor.a;
			dither31 = step( dither31, saturate( ( saturate( ( _DitherOpacity * CAMFADE44 ) ) * VCA33 ) * 1.00001 ) );
			float Dither38 = saturate( dither31 );
			clip( Dither38 - _Cutoff );
		}

		ENDCG
	}
	Fallback Off
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19909
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;53;-3888,592;Inherit;False;1839.347;565.0765;Camera Depth Fade;12;44;54;60;50;59;43;58;48;49;57;56;55;Camera Depth Fade;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;56;-3840,896;Inherit;False;Property;_CameraFadeFarLength;Camera Fade Far Length;4;0;Create;True;0;0;0;False;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;55;-3840,1024;Inherit;False;Property;_CameraFadeFarOffset;Camera Fade Far Offset;5;0;Create;True;0;0;0;False;0;False;20;20;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CameraDepthFade, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;57;-3456,896;Inherit;False;3;2;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;49;-3840,768;Inherit;False;Property;_CameraFadeOffset;Camera Fade Offset;3;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;48;-3840,640;Inherit;False;Property;_CameraFadeLength;Camera Fade Length;2;0;Create;True;0;0;0;False;3;Space(33);Header(Camera Fade);Space(13);False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;58;-3200,896;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CameraDepthFade, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;43;-3456,640;Inherit;False;3;2;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;59;-3072,896;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;50;-3200,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;60;-2816,896;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;54;-2816,640;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;40;-4400,-48;Inherit;False;2355.015;431.3048;Dither;10;38;37;31;34;35;36;42;52;45;32;Dither;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;44;-2304,640;Inherit;False;CAMFADE;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;74;-1968,-48;Inherit;False;1572;1050.7;Base;9;28;33;25;26;27;61;17;18;11;Base;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;32;-4352,0;Inherit;False;Property;_DitherOpacity;Dither Opacity;1;0;Create;True;0;0;0;False;3;Space(33);Header(Dither);Space(13);False;1;0.9;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;45;-4352,128;Inherit;False;44;CAMFADE;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;33;-1920,384;Inherit;False;VCA;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;52;-3968,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;42;-3584,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;36;-3072,128;Inherit;False;33;VCA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;17;-1920,512;Inherit;False;Property;_Color;Color;7;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;11;-1920,768;Inherit;True;Property;_MainTexture;Main Texture;6;0;Create;True;0;0;0;False;3;Space(33);Header(Main Texture);Space(13);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;35;-3072,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;34;-2816,128;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;73;-944,-432;Inherit;False;548;306.95;Emission;3;29;30;63;Emission;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;72;-1200,1104;Inherit;False;804;418.95;Metal;6;19;20;21;65;22;67;Metal;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;71;-3248,1232;Inherit;False;1188;538.7;Normal;5;15;14;23;12;69;Normal;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;26;-1408,0;Inherit;False;Property;_FlatColor;Flat Color;8;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;18;-1408,512;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;28;-1920,128;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DitherNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;31;-2816,0;Inherit;False;1;True;4;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;2;SAMPLER2D;;False;3;SAMPLERSTATE;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;20;-1152,1280;Inherit;False;Property;_SmoothnessMin;Smoothness Min;9;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;21;-1152,1408;Inherit;False;Property;_SmoothnessMax;Smoothness Max;10;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;30;-880,-240;Inherit;False;Property;_Emission;Emission;14;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;25;-1408,128;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;15;-3200,1280;Inherit;False;Constant;_Vector0;Vector 0;2;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;23;-2816,1408;Inherit;False;Property;_NormalIntensity;Normal Intensity;13;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;12;-3200,1536;Inherit;True;Property;_Normal;Normal;12;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;37;-2560,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;19;-896,1280;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;22;-1152,1152;Inherit;False;Property;_Metallic;Metallic;11;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;29;-896,-384;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;27;-1152,128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;14;-2816,1280;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;38;-2304,0;Inherit;False;Dither;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;65;-640,1280;Inherit;False;SMO;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;-640,-384;Inherit;False;EM;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;-640,128;Inherit;False;MCOL;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;67;-640,1152;Inherit;False;MET;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;69;-2304,1280;Inherit;False;NORM;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;62;-256,128;Inherit;False;61;MCOL;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;64;-256,384;Inherit;False;63;EM;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;39;-256,448;Inherit;False;38;Dither;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;66;-256,320;Inherit;False;65;SMO;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;70;-256,192;Inherit;False;69;NORM;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;68;-256,256;Inherit;False;67;MET;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;75;-16,96;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;0;Standard;Vefects/SH_Vefects_BIRP_VFX_Opaque_Dither_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Masked;0.5;True;True;0;False;TransparentCutout;;AlphaTest;ForwardOnly;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;57;0;56;0
WireConnection;57;1;55;0
WireConnection;58;0;57;0
WireConnection;43;0;48;0
WireConnection;43;1;49;0
WireConnection;59;0;58;0
WireConnection;50;0;43;0
WireConnection;60;0;59;0
WireConnection;54;0;50;0
WireConnection;54;1;60;0
WireConnection;44;0;54;0
WireConnection;33;0;28;4
WireConnection;52;0;32;0
WireConnection;52;1;45;0
WireConnection;42;0;52;0
WireConnection;35;0;42;0
WireConnection;35;1;36;0
WireConnection;18;0;17;5
WireConnection;18;1;11;5
WireConnection;31;0;35;0
WireConnection;31;1;34;0
WireConnection;25;0;18;0
WireConnection;25;1;17;5
WireConnection;25;2;26;0
WireConnection;37;0;31;0
WireConnection;19;0;21;0
WireConnection;19;1;20;0
WireConnection;19;2;11;2
WireConnection;29;0;27;0
WireConnection;29;1;30;0
WireConnection;27;0;25;0
WireConnection;27;1;28;0
WireConnection;14;0;15;0
WireConnection;14;1;12;5
WireConnection;14;2;23;0
WireConnection;38;0;37;0
WireConnection;65;0;19;0
WireConnection;63;0;29;0
WireConnection;61;0;27;0
WireConnection;67;0;22;0
WireConnection;69;0;14;0
WireConnection;75;0;62;0
WireConnection;75;1;70;0
WireConnection;75;2;64;0
WireConnection;75;3;68;0
WireConnection;75;4;66;0
WireConnection;75;10;39;0
ASEEND*/
//CHKSM=24B3DB87A9DA87817317290BFE3B51EC2115C87F