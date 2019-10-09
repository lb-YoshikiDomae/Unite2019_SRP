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
	//
	// <comment>
	// 本ソースでは流れを分かりやすくするため、描画関数のみにしてあります
	// 各処理はlbRenderPipelineInstance_sub.csを参照ください！
	//
	protected override void Render( ScriptableRenderContext context, Camera[] cameras )
	{
		// カメラごとに処理
		foreach ( var camera in cameras )
		{
			// コマンドバッファを用意
			CommandBuffer	cb = new CommandBuffer();

			//
			// <comment>
			// カメラプロパティ設定です
			// ここは定型文的な感じになるかと思います
			//
			context.SetupCameraProperties( camera );

			//
			// <comment>
			// カリング処理を行います
			// ここは定型文的な感じになるかと思います
			//
			{
				cullResults = new CullingResults();
				ScriptableCullingParameters	cullingParameters;
				if ( !camera.TryGetCullingParameters( false, out cullingParameters ) ) continue;

				cullingParameters.shadowDistance = Mathf.Min( 10.0f, camera.farClipPlane );		// シャドウの為の仮措置

				cullResults = context.Cull( ref cullingParameters );
			}

			//
			// <comment>
			// 各種RenderTextureを用意します
			// camera.TargetTextureには直接書き込まず、これらのRenderTextureに書いてから最終的にコピーするだけです
			//
			CreateRenderTexture( context, camera, cb );

			//
			// <comment>
			// シャドウマップのセットアップを行います
			// 講演でもお話しした投影テクスチャシャドウを行うため、R8バッファで作成しています
			//
			SetupShadowMap( context, camera, cb );

			//
			// <comment>
			// 講演内では省きましたが、カメラクリア処理をしないと絵が描けません
			//
			ClearModelRenderTexture( context, camera, cb );

			//
			// <comment>
			// 不透明描画部分です
			// 3Dモデル用RenderTextureに不透明部分だけ書き込みます
			//
			DrawOpaque( context, camera, cb );

			//
			// <comment>
			// 最後の不透明であるSkyboxを書き込みます
			//
			if ( camera.clearFlags == CameraClearFlags.Skybox )
			{
				context.DrawSkybox( camera );
			}

			//
			// <comment>
			// 今回のキモとなるブレンドバッファ描画
			// 小さめのバッファを用意しそこにパーティクルを書きます
			//
			DrawBlendBuffer( context, camera, cb );

			//
			// <comment>
			// パーティクル以外の半透明の描画部分です
			// 今回のシーンでは該当部分が無いので動作未確認です
			//
			DrawTransparent( context, camera, cb );

			//
			// <comment>
			// ここで一通りの絵が書けたので、camera.targetTextureにフィードバックします
			//
			RestoreCameraTarget( context, cb );

#if	UNITY_EDITOR
			//
			// <comment>
			// Gizmo描画その1です
			// イメージエフェクト前に描かないといけないものが該当するらしいです
			// (調査あまり出来ていません)
			//
			if ( UnityEditor.Handles.ShouldRenderGizmos() )		// ←この判定をしないとGameビューでも描かれてしまう！
			{
				context.DrawGizmos( camera, GizmoSubset.PreImageEffects );
			}
#endif

			//
			// <comment>
			// ここにポストフィルタの処理が挟まることになると思います
			//

#if	UNITY_EDITOR
			//
			// <comment>
			// Gizmo描画その1です
			// イメージエフェクト後に描かないといけないものが該当するらしいです
			// (調査あまり出来ていません)
			//
			if ( UnityEditor.Handles.ShouldRenderGizmos() )		// ←この判定をしないとGameビューでも描かれてしまう！
			{
				context.DrawGizmos( camera, GizmoSubset.PostImageEffects );
			}
#endif

			//
			// <comment>
			// UIを描画します
			// 講演でお話しした通り、デフォルトUIシェーダも使えるようになっているはず
			// (ただSceneビューではなぜか表示されず。。LWRPでは出るのに。。)
			//
			// あとCanvasが「Screen Space - Overlay」のものはここでは書かれません
			// SRPで処理せず、全く別のところで描かれるようです
			//
			DrawUI( context, camera, cb );

			//
			// <comment>
			// 利用したRenderTextureを破棄します
			//
			ReleaseRenderTexture( context, cb );
		}

		// コンテキストのサブミット
		context.Submit();
	}
}
