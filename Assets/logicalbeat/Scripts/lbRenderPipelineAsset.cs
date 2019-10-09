using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if	UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

//
// <comment>
// こちらはGraphics Settingsに設定するSRPのバイナリに関するクラスです
//
public class lbRenderPipelineAsset : RenderPipelineAsset
{
	//
	// <comment>
	// Assetに情報として含まれるデフォルトマテリアル情報
	// 今回はスタンダードなもの、UI、パーティクル用のを用意
	//
	[SerializeField] private	Material	materialStandard;
	[SerializeField] private	Material	materialParticle;
	[SerializeField] private	Material	materialUI;

	//
	// <comment>
	// 内部でのパイプライン作成処理
	// （お約束的な処理）
	//
	protected override RenderPipeline CreatePipeline()
	{
		// インスタンスを作るだけ！！
		return	new lbRenderPipelineInstance( this );
	}

#if	UNITY_EDITOR
	//
	// <comment>
	// 右クリックメニューでRenderPipelineAssetを作るための処理
	// 割とここもお約束的に書いています
	//
	internal class CreatePipelineAsset : EndNameEditAction
	{
		// マテリアルを検索
		public static Type	FindMaterial<Type>( string name ) where Type : Object
		{
			Type resourceAsset;
			var guids = AssetDatabase.FindAssets( name + " t:" + typeof(Type).Name, null );
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath( guid );
				if ( path.IndexOf( "/logicalbeat/" ) >= 0 ) {
					resourceAsset = AssetDatabase.LoadAssetAtPath<Type>( path );
					return	( resourceAsset );
				}
			}
			return	( null );
		}

		public override void Action(int instanceId, string pathName, string resourceFile)
		{
			var instance = CreateInstance<lbRenderPipelineAsset>();

			// Assetに直指定しても良いが、面倒なので探して記憶させておく
			instance.materialStandard = FindMaterial<Material>( "lbStandard" );
			instance.materialParticle = FindMaterial<Material>( "lbParticle" );
			instance.materialUI       = FindMaterial<Material>( "lbUI-Default" );

			AssetDatabase.CreateAsset(instance, pathName);
		}
	}
	[MenuItem("Assets/Create/Rendering/lb Render Pipeline Asset", priority = CoreUtils.assetCreateMenuPriority1)]
	private static void CreatePipelineAssetFileMenu()
	{
		ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreatePipelineAsset>(),
			"lbRenderPipelineAsset.asset", null, null);
	}
#endif

#if	UNITY_EDITOR
	//
	// <comment>
	// デフォルトマテリアル＆シェーダの設定
	// 今回は最低限にしてあります＆用途不明のものもいくつかあります
	//
	public override Shader defaultShader
	{
		get
		{
			if ( materialStandard != null ) {
				return	( materialStandard.shader );
			} else {
				return	( base.defaultShader );
			}
		}
	}

	public override Material defaultMaterial
	{
		get
		{
			if ( materialStandard != null ) {
				return	( materialStandard );
			} else {
				return	( base.defaultMaterial );
			}
		}
	}

	public override Material defaultParticleMaterial
	{
		get
		{
			if ( materialParticle != null ) {
				return	( materialParticle );
			} else {
				return	( base.defaultParticleMaterial );
			}
		}
	}

	public override Material defaultUIMaterial
	{
		get
		{
			if ( materialUI != null ) {
				return	( materialUI );
			} else {
				return	( base.defaultUIMaterial );
			}
		}
	}

//	public override Material defaultLineMaterial
//	{
//		get	{	return	( base.defaultLineMaterial );	}
//	}

//	public override Material defaultTerrainMaterial
//	{
//		get	{	return	( base.defaultTerrainMaterial );	}
//	}

//	public override Material defaultUIOverdrawMaterial
//	{
//		get	{	return	( base.defaultUIOverdrawMaterial );	}
//	}

//	public override Material defaultUIETC1SupportedMaterial
//	{
//		get	{	return	( base.defaultUIETC1SupportedMaterial );	}
//	}

//	public override Material default2DMaterial
//	{
//		get	{	return	( base.default2DMaterial );	}
//	}
#endif
}
