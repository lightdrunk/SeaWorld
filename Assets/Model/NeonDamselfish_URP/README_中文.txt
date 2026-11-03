霓虹雀鲷 / Neon Damselfish — Unity URP 教学资源
学名：Pomacentrus coelestis

导入 Unity
1. 使用已经配置好 Universal Render Pipeline 的项目。配置脚本以 Unity 2022.3+ 的 API 编写。
2. 把本包的 Assets/NeonDamselfish 整个文件夹复制到项目的 Assets 下，保留 Textures、Materials、Editor 和 .meta 文件。
3. 等 Unity 完成导入和脚本编译。脚本会配置 Generic 骨骼、法线贴图、URP/Lit 材质，并生成 Prefab 和 Animator Controller。
4. 将 Assets/NeonDamselfish/Prefabs/NeonDamselfish_URP.prefab 拖入场景。Prefab 生成后会播放 IdleSwim_2s 循环。
5. 如未自动生成，点击 Tools > Neon Damselfish > Setup URP Material and Prefab。
   脚本仅处理 Assets/NeonDamselfish，不会切换项目渲染管线。
   配置过程保留已有 Prefab；要重新生成，可先移走本资源生成的 Prefab，再运行菜单。

仅使用 FBX 时
模型文件：Assets/NeonDamselfish/NeonDamselfish_Rigged.fbx
请同时复制 Textures；FBX 不内嵌贴图。FBX 本身不存储 Unity 专用 URP 着色器。
本包已提供 Materials/PC_Atlas_URP.mat；导入脚本负责将其映射给 FBX。
手动设置：Rig=Generic；Scale Factor=1，Use File Scale 开启；保留层级，不启用 Optimize Game Objects。
材质 Shader=Universal Render Pipeline/Lit，Surface=Opaque，Workflow=Metallic。
Base Map → PC_BaseColor_2K.png（sRGB 开）
Normal Map → PC_Normal_2K.png（Texture Type=Normal map，sRGB 关，Strength=0.7）
Metallic Map → PC_URP_MetallicSmoothness_2K.png（sRGB 关）
Smoothness Source=Metallic Alpha，Smoothness=1。R=金属度，A=1−粗糙度。
PC_Roughness_2K.png、PC_Metallic_2K.png 为独立原始通道，均为线性数据。
法线为切线空间 +Y。金属度为 0，鱼体按非金属材质处理。

模型规格
标准体长 72 mm；全长约 93.79 mm；体高约 28.24 mm。
标准体长 / 体高 = 2.55000。
19,389 个顶点，37,806 个三角面，39 个独立网格，17 根骨骼，17 个标注锚点。
主体和鳍膜以四边面为主，细节包含三角面和封口面；引擎导入后会三角化。
1 单位=1 米；Unity 导入后 Y 向上，鱼头朝 −X；需要教学放大时等比缩放根节点。
每顶点最多 2 个骨骼影响。脊柱、头、下颌、胸鳍、背鳍、臀鳍、尾柄、尾鳍、腹鳍均可控制。
IdleSwim_2s：24 fps，首尾中性姿态，左右摆尾、轻微胸鳍扇动。下颌已绑定，可另行动画。

形态与配色
背鳍 XIII + 14；臀鳍 II + 14；胸鳍每侧 18 条；侧线孔鳞每侧 18 个。
两颌各有内外两列圆锥状牙齿。尾鳍为尖叶叉形；背鳍软条部不拉长。
遵照后续提供的参考照片调色：钴蓝／电蓝背区、偏灰金黄腹区、浅奶黄色尾鳍。
腹部黄色范围偏下，保留清晰而略柔和的分界，不使用全腹高饱和橙黄色。
配色贴图由程序重建，未使用参考照片的像素、背景或水印。
栉鳞表面通过基础色与法线贴图表现，含后缘细梳状纹；侧线孔位、鳍棘、鳍条和牙齿为几何结构。
鳍膜为闭合薄壳，使用不透明 PBR 材质和浅色膜面；没有玻璃式透明混合。

标注与结构
层级中 Label_ 开头的空节点为锚点，已绑定到相应骨骼。
BlueDorsalRegion 蓝色背区；YellowVentralRegion 黄色腹区；BlueYellowBoundary 蓝黄分界。
ForkedCaudalFin 叉形尾鳍；DorsalSpines_XIII 背鳍硬棘；DorsalSoftRays_14 背鳍软条。
AnalFin_II_14 臀鳍；PectoralFin_L/R 胸鳍；TerminalMouth 端位口。
Eye_L/R 眼睛；Operculum_L/R 鳃盖；CtenoidScales 栉鳞；LateralLine18_L 侧线孔鳞。
网格亦按上述结构命名，便于单独选取与高亮。
AnatomyManifest.json 提供结构清单、鳍条数量、骨骼关系和锚点位置。
JSON 坐标为 Blender 的米制 Z 向上坐标；Unity 标注请直接读取导入后的 Label_ Transform。

验证范围
已用 Blender 4.5.3 导出并重新导入 FBX 7400，验证网格、17 根骨骼、全部蒙皮、动画与贴图路径。
验证无未加权顶点、无退化三角面、无非流形边；左右几何对称。
UV 位于 0–1 内，2K 内区像素采样检测重叠为 0；左右体侧使用独立 UV。
所有贴图为 2048×2048 PNG；金属度为 0；打包光滑度与 1−粗糙度匹配，最大量化误差 1/255。
本机未安装 Unity Editor，URP 材质和 C# 配置脚本尚未在 Unity 编辑器中编译、渲染实测。
Preview_*.png 为 Blender 预览，不是 Unity 截图。项目灯光和曝光会影响最终观感。
详细数值见 ValidationReport.json。

附加文件
NeonDamselfish_Rigged.glb：带贴图与骨骼的便携预览模型。
NeonDamselfish_Source.blend：可编辑 Blender 源文件，已打包贴图；相机、灯光仅用于预览。
Preview_Side.png / Preview_ThreeQuarter.png：最终几何与贴图的预览。

参考来源
形态参数：台湾鱼类数据库 https://fishdb.sinica.edu.tw/taxon/382384-fishdb
物种外观：Australian Museum https://australian.museum/learn/animals/fishes/neon-damsel-pomacentrus-coelestis/
用户配色图：https://en.pimg.jp/139/192/274/1/139192274.jpg
URP/Lit：https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/lit-shader.html
URP/Lit Shader GUID：Unity-Technologies/Graphics 官方仓库的 Lit.shader.meta。
