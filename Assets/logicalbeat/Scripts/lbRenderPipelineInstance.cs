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
		}

		// コンテキストのサブミット
		context.Submit();
	}
}
