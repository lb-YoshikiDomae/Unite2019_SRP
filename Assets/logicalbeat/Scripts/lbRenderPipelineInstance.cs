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
	private Material	materialCopyDepth        = null;		// デプスバッファコピー用マテリアル
	private Material	materialMergeBlendBuffer = null;		// ブレンドバッファ合成用マテリアル

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

	// 描画処理
	protected override void Render( ScriptableRenderContext context, Camera[] cameras )
	{
		// カメラごとに処理
		foreach ( var camera in cameras )
		{
			// カメラプロパティ設定
			context.SetupCameraProperties( camera );

			// カリング処理
			CullingResults				cullResults = new CullingResults();
			ScriptableCullingParameters	cullingParameters;
			if ( !camera.TryGetCullingParameters( false, out cullingParameters ) ) continue;
			cullResults = context.Cull( ref cullingParameters );

			// コマンドバッファを用意
			CommandBuffer	cb = new CommandBuffer();

			// RenderTextureを用意
			int	rt_width     = (int)( (float)Screen.width  * 0.7f );
			int	rt_height    = (int)( (float)Screen.height * 0.7f );
			int	blend_width  = (int)( (float)rt_width      * 0.5f );
			int	blend_height = (int)( (float)rt_height     * 0.5f );
			int	rt_targetTexture = 0;
			int	rt_depthTexture  = 1;
			int	rt_blendTexture  = 2;
			int	rt_blendDepth    = 3;

			// 各種準備
			{
				// コマンドバッファ利用の準備
				cb.Clear();

				{
					cb.GetTemporaryRT( rt_targetTexture, rt_width,    rt_height,     0, FilterMode.Bilinear, RenderTextureFormat.Default );
					cb.GetTemporaryRT( rt_depthTexture,  rt_width,    rt_height,    24, FilterMode.Point,    RenderTextureFormat.Depth   );
					cb.GetTemporaryRT( rt_blendTexture,  blend_width, blend_height,  0, FilterMode.Bilinear, RenderTextureFormat.Default );
					cb.GetTemporaryRT( rt_blendDepth,    blend_width, blend_height, 24, FilterMode.Point,    RenderTextureFormat.Depth   );
				}

				// 書き込み先を少し小さめのRenderTextureに変更
				cb.SetRenderTarget( new RenderTargetIdentifier( rt_targetTexture ), new RenderTargetIdentifier( rt_depthTexture  ) );

				// デプスバッファクリア
//				cb.ClearRenderTarget( true, false, Color.black, 1.0f );
				cb.ClearRenderTarget( true, true,  Color.black, 1.0f );		// 分かりやすくするためテスト的にカラーもクリア

				// ここまでのコマンドバッファ実行
				context.ExecuteCommandBuffer( cb );
			}

			// 不透明描画
			{
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

			// Skybox描画
			if ( camera.clearFlags == CameraClearFlags.Skybox ) context.DrawSkybox( camera );

			// ブレンドバッファ描画
			{
				// 書き込み先をブレンドバッファに
				if ( ( materialMergeBlendBuffer != null ) && ( materialCopyDepth != null ) )
				{
					cb.Clear();
					cb.Blit( new RenderTargetIdentifier( rt_depthTexture ), new RenderTargetIdentifier( rt_blendDepth ), materialCopyDepth );
					cb.SetRenderTarget( new RenderTargetIdentifier( rt_blendTexture ), new RenderTargetIdentifier( rt_blendDepth  ) );
					cb.ClearRenderTarget( false, true, Color.black, 1.0f );		// ブレンドバッファのカラー初期値は(0,0,0,1)
					context.ExecuteCommandBuffer( cb );
				}

				// フィルタリング＆ソート設定
				SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CommonTransparent };
				var	settings = new DrawingSettings( new ShaderTagId( "lbParticle" ), sortingSettings );		// LightMode = lbParticleのところを描く
				settings.perObjectData = PerObjectData.ReflectionProbes;
				var	filterSettings = new FilteringSettings(
										new RenderQueueRange( (int)RenderQueue.GeometryLast, (int)RenderQueue.Transparent ),			// Queue = 2500～3000まで
										camera.cullingMask
									);

				// 描画処理
				context.DrawRenderers( cullResults, ref settings, ref filterSettings );

				// 書き込み先を戻してブレンドバッファを合成
				if ( ( materialMergeBlendBuffer != null ) && ( materialCopyDepth != null ) )
				{
					cb.Clear();
					cb.SetRenderTarget( new RenderTargetIdentifier( rt_targetTexture ), new RenderTargetIdentifier( rt_depthTexture  ) );
					cb.Blit( new RenderTargetIdentifier( rt_blendTexture ), new RenderTargetIdentifier( rt_targetTexture ), materialMergeBlendBuffer );
					context.ExecuteCommandBuffer( cb );
				}
			}

			// 半透明描画
			{
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

#if	UNITY_EDITOR
			// Gizmo描画
			context.DrawGizmos( camera, GizmoSubset.PreImageEffects );
			// Gizmo描画
			context.DrawGizmos( camera, GizmoSubset.PostImageEffects );
#endif

			// 各種終了処理
			{
				// コマンドバッファ利用の準備
				cb.Clear();

				// 書き込み先をカメラターゲットに戻してtargetTextureをコピー
				{
					cb.SetRenderTarget( new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget ) );
					cb.Blit( new RenderTargetIdentifier( rt_targetTexture ), new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget ) );
				}

				// 使用したRenderTextureを破棄
				{
					cb.ReleaseTemporaryRT( rt_targetTexture );
					cb.ReleaseTemporaryRT( rt_depthTexture  );
					cb.ReleaseTemporaryRT( rt_blendTexture  );
					cb.ReleaseTemporaryRT( rt_blendDepth    );
				}

				// コマンドバッファ実行
				context.ExecuteCommandBuffer( cb );
			}
		}

		// コンテキストのサブミット
		context.Submit();
	}
}
