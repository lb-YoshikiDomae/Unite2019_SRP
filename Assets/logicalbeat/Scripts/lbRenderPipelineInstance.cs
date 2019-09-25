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
	// コンストラクタ
	public lbRenderPipelineInstance( lbRenderPipelineAsset asset )
	{
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

			// 書き込み先を少し小さめのRenderTextureに変更
			int	rt_width  = (int)( Screen.width  * 0.7f );
			int	rt_height = (int)( Screen.height * 0.7f );
			int	rt_targetTexture = 0;
			int	rt_depthTexture  = 1;
			{
				cb.Clear();
				cb.GetTemporaryRT( rt_targetTexture, rt_width, rt_height,  0, FilterMode.Bilinear, RenderTextureFormat.Default );
				cb.GetTemporaryRT( rt_depthTexture,  rt_width, rt_height, 24, FilterMode.Point,    RenderTextureFormat.Depth   );
				cb.SetRenderTarget(
					new RenderTargetIdentifier( rt_targetTexture ),
					new RenderTargetIdentifier( rt_depthTexture  )
				);
				context.ExecuteCommandBuffer( cb );
			}

			// デプスバッファクリア
			{
				cb.Clear();
//				cb.ClearRenderTarget( true, false, Color.black, 1.0f );
				cb.ClearRenderTarget( true, true, Color.red, 1.0f );
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

			// Gizmo描画
			context.DrawGizmos( camera, GizmoSubset.PreImageEffects );
			// Gizmo描画
			context.DrawGizmos( camera, GizmoSubset.PostImageEffects );

			// 書き込み先をカメラターゲットに戻してtargetTextureをコピー
			{
				cb.Clear();
				cb.SetRenderTarget( new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget ) );
				cb.Blit( new RenderTargetIdentifier( rt_targetTexture ), new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget ) );
				context.ExecuteCommandBuffer( cb );
			}

			// 使用したRenderTextureを破棄
			{
				cb.Clear();
				cb.ReleaseTemporaryRT( rt_targetTexture );
				cb.ReleaseTemporaryRT( rt_depthTexture  );
				context.ExecuteCommandBuffer( cb );
			}
		}

		// コンテキストのサブミット
		context.Submit();
	}
}
