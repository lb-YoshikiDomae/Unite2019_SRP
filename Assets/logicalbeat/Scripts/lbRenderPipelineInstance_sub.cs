using System;
using System.Collections;
using System.Collections.Generic;
#if	UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

public partial class lbRenderPipelineInstance : RenderPipeline
{
	// RenderTexture種別
	private enum	RenderTextureKind
	{
		ModelColor,		// モデル描画用(カラー)
		ModelDepth,		// モデル描画用(デプス)
		BlendColor,		// ブレンドバッファ(カラー)
		BlendDepth,		// ブレンドバッファ(デプス)
		ShadowMap,		// シャドウマップ

		Num,
	};
	private Material					materialCopyDepth        = null;									// デプスバッファコピー用マテリアル
	private Material					materialMergeBlendBuffer = null;									// ブレンドバッファ合成用マテリアル
	private RenderTargetIdentifier[]	RTI = new RenderTargetIdentifier[(int)RenderTextureKind.Num];		// RenderTarget情報
	private CullingResults				cullResults;														// カリング結果

	// コンストラクタ
	public lbRenderPipelineInstance( lbRenderPipelineAsset asset )
	{
		// 初期設定
		if ( materialCopyDepth == null ) {
			Shader	shader = Shader.Find( "Hidden/SRP/CopyDepth" );
			if ( shader != null ) materialCopyDepth = new Material( shader );
		}
		if ( materialMergeBlendBuffer == null ) {
			Shader	shader = Shader.Find( "Hidden/SRP/MergeBlendBuffer" );
			if ( shader != null ) materialMergeBlendBuffer = new Material( shader );
		}
	}

	// RenderTextureを作成
	private void CreateRenderTexture( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// コマンドバッファ利用の準備
		cb.Clear();

		// カメラのtargetTextureのサイズを取得
		int	width, height;
		if ( camera.targetTexture == null ) {
			width  = Screen.width;
			height = Screen.height;
		} else {
			width  = camera.targetTexture.width;
			height = camera.targetTexture.height;
		}

		// RenderTextureのサイズ
		int	rt_model_width  = (int)( (float)width           * 0.7f );		// モデル描画用のバッファは本来の縦横70%サイズ
		int	rt_model_height = (int)( (float)height          * 0.7f );
		int	rt_blend_width  = (int)( (float)rt_model_width  * 0.5f );		// ブレンドバッファはモデル描画用バッファの縦横50%サイズ
		int	rt_blend_height = (int)( (float)rt_model_height * 0.5f );

		// 一枚ずつ作成(明示的にカラーとデプスを分ける)
		cb.GetTemporaryRT( (int)RenderTextureKind.ModelColor, rt_model_width, rt_model_height,  0, FilterMode.Bilinear, RenderTextureFormat.Default );
		cb.GetTemporaryRT( (int)RenderTextureKind.ModelDepth, rt_model_width, rt_model_height, 24, FilterMode.Point,    RenderTextureFormat.Depth   );
		cb.GetTemporaryRT( (int)RenderTextureKind.BlendColor, rt_blend_width, rt_blend_height,  0, FilterMode.Bilinear, RenderTextureFormat.Default );
		cb.GetTemporaryRT( (int)RenderTextureKind.BlendDepth, rt_blend_width, rt_blend_height, 24, FilterMode.Point,    RenderTextureFormat.Depth   );
		cb.GetTemporaryRT( (int)RenderTextureKind.ShadowMap,  1024,           1024,             0, FilterMode.Bilinear, RenderTextureFormat.R8      );		// シャドウマップは固定サイズで

		// ここまでのコマンドバッファ実行
		context.ExecuteCommandBuffer( cb );

		// RenderTargetIdentifierを作っておく
		for (int h = 0;h < (int)RenderTextureKind.Num;++h) {
			RTI[h] = new RenderTargetIdentifier( h );
		}
	}

	// シャドウマップのセットアップ
	private void SetupShadowMap( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// コマンドバッファ利用の準備
		cb.Clear();

		//
		// <comment>
		// ライト情報を取得しています
		// 今回は最初に見つかった平行光源をシャドウライトとして利用します
		//
		int	lightIndex = -1;
		for (int h = 0;h < cullResults.visibleLights.Length;++h) {
			// 今回のライトを取得
			VisibleLight	currLight = cullResults.visibleLights[h];
			Light			light     = currLight.light;
			if ( light == null ) continue;

			// シャドウを落とす？
			if ( light.shadows == LightShadows.None ) continue;
			if ( light.shadowStrength <= 0.0f ) continue;

			// 今回は平行光源のみ
			if ( light.type != LightType.Directional ) continue;

			// バウンド判定
			Bounds	bounds;
			if ( !cullResults.GetShadowCasterBounds( h, out bounds ) ) continue;

			// 使用ライトの決定
			lightIndex = h;
			break;
		}

		// もしシャドウが無さそうな時はダミーテクスチャを設定して終わり
		if ( lightIndex < 0 ) {
			cb.SetGlobalTexture( "_ShadowMap", Texture2D.whiteTexture );
			context.ExecuteCommandBuffer( cb );
			return;
		}

		// 各種パラメータの算出と設定
		ShadowSplitData	shadowSplitData;
		{
			// パラメータ算出
			Matrix4x4		viewMatrix;	
			Matrix4x4		projMatrix;	
			cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				lightIndex,									// int activeLightIndex,
				0,											// int splitIndex,
				1,											// int splitCount,
				new Vector3(1.0f, 0.0f, 0.0f),				// Vector3 splitRatio,
				1024,										// int shadowResolution,
				QualitySettings.shadowNearPlaneOffset,		// float shadowNearPlaneOffset,
				out viewMatrix,								// out Matrix4x4 viewMatrix,
				out projMatrix,								// out Matrix4x4 projMatrix,
				out shadowSplitData							// out Experimental.Rendering.ShadowSplitData shadowSplitData
			);

			// マトリクスを作ってグローバル化
			Matrix4x4	matView        = viewMatrix;
			Matrix4x4	matProj        = projMatrix;
			Matrix4x4	matProj_shader = GL.GetGPUProjectionMatrix( matProj, true );
			Matrix4x4	matVP          = matProj_shader * matView;
			cb.SetGlobalMatrix( "_ShadowLightView", matView );
			cb.SetGlobalMatrix( "_ShadowLightProj", matProj_shader );
			cb.SetGlobalMatrix( "_ShadowLightVP", matVP );

			// シャドウマップのグローバル化
			cb.SetGlobalTexture( "_ShadowMap", RTI[(int)RenderTextureKind.ShadowMap] );
		}

		// RenderTarget設定(R8フォーマットのシャドウマップ)してクリア処理
		cb.SetRenderTarget( RTI[(int)RenderTextureKind.ShadowMap] );
		cb.ClearRenderTarget( true, true, Color.white, 1.0f );
		context.ExecuteCommandBuffer( cb );

		// 描画
		var	settings = new ShadowDrawingSettings( cullResults, lightIndex );
		settings.splitData = shadowSplitData;
		context.DrawShadows( ref settings );
	}

	// モデル用レンダーテクスチャクリア
	private void ClearModelRenderTexture( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// コマンドバッファ利用の準備
		cb.Clear();

		// 3Dモデル用RenderTextureを設定
		cb.SetRenderTarget( RTI[(int)RenderTextureKind.ModelColor], RTI[(int)RenderTextureKind.ModelDepth] );

		// 状況に応じてクリア内容を分岐
		if ( false
			|| ( camera.clearFlags == CameraClearFlags.Depth  )
			|| ( camera.clearFlags == CameraClearFlags.Skybox )
		)
		{
			cb.ClearRenderTarget( true, false, Color.black, 1.0f );		// デプスのみクリア
		}
		else
		if ( camera.clearFlags == CameraClearFlags.SolidColor )
		{
			cb.ClearRenderTarget( true, true, camera.backgroundColor, 1.0f );		// カラーとデプスをクリア
		}

		// ここまでのコマンドバッファ実行
		context.ExecuteCommandBuffer( cb );
	}

	// 不透明部分描画
	private void DrawOpaque( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// 3Dモデル用RenderTextureを設定
		cb.Clear();
		cb.SetRenderTarget( RTI[(int)RenderTextureKind.ModelColor], RTI[(int)RenderTextureKind.ModelDepth] );
		context.ExecuteCommandBuffer( cb );

		// フィルタリング＆ソート設定
		SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CommonOpaque };
		var	settings = new DrawingSettings( new ShaderTagId( "lbForward" ), sortingSettings );		// LightMode = lbForwardのところを描く
		settings.perObjectData = PerObjectData.ReflectionProbes;
		var	filterSettings = new FilteringSettings(
								new RenderQueueRange( 0, (int)RenderQueue.GeometryLast ),			// Queue = 0～2500まで
								camera.cullingMask
							);

		// 描画処理
		context.DrawRenderers( cullResults, ref settings, ref filterSettings );
	}

	// ブレンドバッファへの描画
	private void DrawBlendBuffer( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// フィルタリング＆ソート設定
		SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CommonTransparent };
		var	settings = new DrawingSettings( new ShaderTagId( "lbParticle" ), sortingSettings );		// LightMode = lbParticleのところを描く
		settings.perObjectData = PerObjectData.ReflectionProbes;
		var	filterSettings = new FilteringSettings(
								new RenderQueueRange( (int)RenderQueue.GeometryLast, (int)RenderQueue.Transparent ),			// Queue = 2500～3000まで
								camera.cullingMask
							);

		//
		// <comment>
		// ここでSRPに対する追加要望がありますが。。。
		// フィルタリングの結果、描画するRendererが無さそうかどうかチェックする機能が欲しいです
		// もし同機能が実現できればブレンドバッファ周りの設定は丸々省略でき、無駄なデプスコピーなどが省けるからです
		// (GPU負荷が逆に読みづらくなりそうですが、とことん削減したい場合はそうしたい)
		//
		// 現段階では、フラグだけ用意してお茶を濁すことにします
		//
		bool	drawBB = true;
		if ( !drawBB ) return;		// 書く物がなければここで抜けたい！

		//
		// <comment>
		// ブレンドバッファを実現するためにデプスコピーや合成用シェーダ（マテリアル）が必要になります
		// それらが無かったり、他に何かしらの理由でブレンドバッファが実現できない可能性があります
		// その際はモデルバッファに書いてしまうということをしたいので、それの判断を行っています
		//
		bool	enableBB = ( true
			&& ( materialMergeBlendBuffer != null )
			&& ( materialCopyDepth        != null )
			/* デプスをテクスチャとして扱えるハードか？というのも判断した方が良いかと思うが割愛 */
		);

		// 書き込み先をブレンドバッファに変更＆準備
		cb.Clear();
		if ( enableBB )
		{
			// ブレンドバッファが利用できる時はデプスコピーなどを行う
			cb.Blit( RTI[(int)RenderTextureKind.ModelDepth], RTI[(int)RenderTextureKind.BlendDepth], materialCopyDepth );		// モデルRTのデプスをブレンドバッファのデプスにコピー
			cb.SetRenderTarget( RTI[(int)RenderTextureKind.BlendColor], RTI[(int)RenderTextureKind.BlendDepth] );
			cb.ClearRenderTarget( false, true, Color.black/*new Color( 0.0f, 0.0f, 0.0f, 1.0f )*/, 1.0f );		// ブレンドバッファのカラー初期値は(0,0,0,1)
		}
		else
		{
			// ブレンドバッファが利用できない時はモデル用バッファに描くしかない
			cb.SetRenderTarget( RTI[(int)RenderTextureKind.ModelColor], RTI[(int)RenderTextureKind.ModelDepth] );
		}
		context.ExecuteCommandBuffer( cb );

		// 描画処理
		context.DrawRenderers( cullResults, ref settings, ref filterSettings );

		// 書き込み先を戻してブレンドバッファを合成
		if ( enableBB )
		{
			cb.Clear();
			cb.SetRenderTarget( RTI[(int)RenderTextureKind.ModelColor], RTI[(int)RenderTextureKind.ModelDepth] );
			cb.Blit( RTI[(int)RenderTextureKind.BlendColor], RTI[(int)RenderTextureKind.ModelColor], materialMergeBlendBuffer );
			context.ExecuteCommandBuffer( cb );
		}
	}

	// 半透明部分描画
	private void DrawTransparent( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// 3Dモデル用RenderTextureを設定
		cb.Clear();
		cb.SetRenderTarget( RTI[(int)RenderTextureKind.ModelColor], RTI[(int)RenderTextureKind.ModelDepth] );
		context.ExecuteCommandBuffer( cb );

		// フィルタリング＆ソート設定
		SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CommonTransparent };
		var	settings = new DrawingSettings( new ShaderTagId( "lbForward" ), sortingSettings );		// LightMode = lbForwardのところを描く
		settings.perObjectData = PerObjectData.ReflectionProbes;
		var	filterSettings = new FilteringSettings(
								new RenderQueueRange( (int)RenderQueue.GeometryLast, (int)RenderQueue.Transparent ),			// Queue = 2500～3000まで
								camera.cullingMask
							);

		// 描画処理
		context.DrawRenderers( cullResults, ref settings, ref filterSettings );
	}

	// カメラターゲットへのフィードバック
	private void RestoreCameraTarget( ScriptableRenderContext context, CommandBuffer cb )
	{
		// コマンドバッファ利用の準備
		cb.Clear();

		// 書き込み先の用意
		RenderTargetIdentifier	cameraTarget = new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget );

		// カメラターゲットへBlit
		{
			cb.SetRenderTarget( cameraTarget );
			cb.Blit( RTI[(int)RenderTextureKind.ModelColor], cameraTarget );
		}

#if	UNITY_EDITOR
		// ギズモでデプステクスチャを参照しているもの(グリッドなど)があるので、エディタ時はデプスも戻します
		if ( materialCopyDepth != null )
		{
			cb.SetRenderTarget( cameraTarget );
			cb.Blit( RTI[(int)RenderTextureKind.ModelDepth], cameraTarget, materialCopyDepth );		// モデルRTのデプスをカメラデプスにコピー
		}
#endif

		// コマンドバッファ実行
		context.ExecuteCommandBuffer( cb );
	}

	// UI部分描画
	private void DrawUI( ScriptableRenderContext context, Camera camera, CommandBuffer cb )
	{
		// 書き込み先の設定
		cb.Clear();
		RenderTargetIdentifier	cameraTarget = new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget );
		cb.SetRenderTarget( cameraTarget );
		context.ExecuteCommandBuffer( cb );

		// フィルタリング＆ソート設定
		SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CanvasOrder };
		var	settings = new DrawingSettings( new ShaderTagId( "SRPDefaultUnlit" ), sortingSettings );		// LightMode = SRPDefaultUnlit（つまり無指定）のところを描く
		settings.perObjectData = PerObjectData.ReflectionProbes;
		var	filterSettings = new FilteringSettings(
								new RenderQueueRange( (int)RenderQueue.Transparent, (int)RenderQueue.Overlay ),			// Queue = 3000～4000まで
								camera.cullingMask
							);

		// 描画処理
		context.DrawRenderers( cullResults, ref settings, ref filterSettings );
	}

	// RenderTextureを破棄
	private void ReleaseRenderTexture( ScriptableRenderContext context, CommandBuffer cb )
	{
		// コマンドバッファ利用の準備
		cb.Clear();

		// 使用したRenderTextureを破棄
		for (int h = 0;h < (int)RenderTextureKind.Num;++h) {
			cb.ReleaseTemporaryRT( h );
		}

		// コマンドバッファ実行
		context.ExecuteCommandBuffer( cb );
	}

}
