using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if	UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

public class lbRenderPipelineAsset : RenderPipelineAsset
{
	[SerializeField] private	Material	materialStandard;
	[SerializeField] private	Material	materialUI;
	[SerializeField] private	Texture2D	textureNoise;
	public Texture2D	TextureNoise
	{
		get { return ( textureNoise ); }
	}


	// 内部でのパイプライン作成処理
	protected override RenderPipeline CreatePipeline()
	{
		return	new lbRenderPipelineInstance( this );
	}

#if	UNITY_EDITOR
	// アセット作成
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

			instance.materialStandard = FindMaterial<Material>( "lbStandard" );
			instance.materialUI       = FindMaterial<Material>( "lbUI-Default" );
			instance.textureNoise     = FindMaterial<Texture2D>( "MarmosetNoise" );

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
		get	{	return	( base.defaultParticleMaterial );	}
	}

	public override Material defaultLineMaterial
	{
		get	{	return	( base.defaultLineMaterial );	}
	}

	public override Material defaultTerrainMaterial
	{
		get	{	return	( base.defaultTerrainMaterial );	}
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

	public override Material defaultUIOverdrawMaterial
	{
		get	{	return	( base.defaultUIOverdrawMaterial );	}
	}

	public override Material defaultUIETC1SupportedMaterial
	{
		get	{	return	( base.defaultUIETC1SupportedMaterial );	}
	}

	public override Material default2DMaterial
	{
		get	{	return	( base.default2DMaterial );	}
	}
#endif
}
