# はじめに
- Unite Tokyo 2019の「SRPで一から描画フローを作ってみた！ ～Unity描画フローからの脱却～」での補足プログラムです。  
講演については↓にスライドと動画が上がってますので参照ください。  
https://learning.unity3d.jp/3284/

# 本プロジェクトについて
## 目的
- 講演内での説明補足としてSRPのシンプルな構成プロジェクトを提供し、SRPに対して理解しやすくするようにするため。
    - ![image](https://user-images.githubusercontent.com/22964712/66531106-95341500-eb45-11e9-90d9-5a523c2cfe02.png)
- 独自SRPを新規構築する取っ掛かりとして活用して欲しいため。
- ※本プロジェクトをベースに、独自SRPを作成頂いて構いません！

## 描画機能について
- 講演後半でのブレンドバッファ周りの環境を持ってきてソース整形とブラッシュアップ、機能追加を行いました。
- 以下の機能を有しています。
    - やや小さめ（縦横70%ずつ）のバッファに3Dモデルを描くように設計
    - 不透明オブジェクト描画（独自Standardシェーダ）
    - ライトはDirectionalを1つのみで、シンプルなLambert計算でのライティング処理
    - ブレンドバッファ法でのパーティクル描画
    - 投影テクスチャシャドウの実装（こちらもDirectionalを1つのみ＆シャドウマップはR8フォーマット）
    - パーティクルシャドウの実装
    - ReflectionProbesの適応（とってもSkyboxの参照のみです）
    - UI描画対応
    - Gizmo描画対応

## 利用方法
1. Projectビューの右クリックメニューで「Create - Rendering - lb Render Pipeline Asset」を選択すると、RenderPipelineAssetが生成されます。  
（本プロジェクトには既に含まれています）
    - <img src="https://user-images.githubusercontent.com/22964712/66531646-6e76de00-eb47-11e9-8dc1-8e4e95825180.png" width=50%>
    - ↓
    - ↓
    - <img src="https://user-images.githubusercontent.com/22964712/66531745-cb729400-eb47-11e9-9643-4c44c1fd96d8.png" width=50%>
2. 出来たRender Pipeline Assetを「Project Settings - Graphics - Scriptable Render Pipeline Settings」に設定すれば適応されます。  
（こちらも本プロジェクトでは既に設定済みです）
    - <img src="https://user-images.githubusercontent.com/22964712/66531822-20160f00-eb48-11e9-8e2d-7ace825d9691.png" width=50%>

## ソース構成など
- 「Assets/logicalbeat」以下にSRP関連のスクリプト、シェーダなどが格納されています。
- 「Assets/logicalbeat/Scripts」が関連スクリプトですが、それぞれ以下の役割になります。

|ファイル名|役割|
|:---|:---|
|lbRenderPipelineAsset.cs|RenderPipelineAssetの生成やデフォルトマテリアルなどの設定を行うための処理が書かれています。|
|lbRenderPipelineInstance.cs|SRP処理部分の本体ですが、読みやすくするためRender関数のみにしています。|
|lbRenderPipelineInstance_sub.cs|↑のRender関数で読んでいる関数などを列挙しています。|

- スクリプトやシェーダ内でコメントに「<comment>」と入れてあるところがあります。  
そこに詳細説明など記してありますので、参考にしてください。
```
※例：シャドウのセットアップ部分

//
// <comment>
// シャドウマップのセットアップを行います
// 講演でもお話しした投影テクスチャシャドウを行うため、R8バッファで作成しています
//
SetupShadowMap( context, camera, cb );
```

# 補足情報
- Unityプロジェクトに対する補足情報を弊社ブログにまとめました。  
併せて参照いただけますと幸いです。  
https://logicalbeat.jp/blog/3971/

# 免責事項
本プロジェクトでの情報を利用することによる損害等に対し、株式会社ロジカルビートは一切の責任を負いません。
