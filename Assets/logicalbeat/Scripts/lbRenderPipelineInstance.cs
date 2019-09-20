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

			// フィルタリング＆ソート設定
			SortingSettings sortingSettings = new SortingSettings( camera ) { criteria = SortingCriteria.CommonOpaque };
			var	settings = new DrawingSettings( new ShaderTagId( "lbDeferred" ), sortingSettings );		// LightMode = lbDeferredのところを描く
			var	filterSettings = new FilteringSettings(
									new RenderQueueRange( 0, (int)RenderQueue.GeometryLast ),			// Queue = 0～3000まで
									camera.cullingMask
								);

			// 描画処理
			context.DrawRenderers( cullResults, ref settings, ref filterSettings );
		}

		// コンテキストのサブミット
		context.Submit();
	}
}
