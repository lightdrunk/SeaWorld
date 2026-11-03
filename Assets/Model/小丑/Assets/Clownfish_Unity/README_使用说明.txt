公子小丑鱼 / Amphiprion ocellaris — Unity FBX 教学模型

交付内容
Ocellaris_Unity.fbx：二进制 FBX，包含模型、蒙皮、16 根骨骼、18 个教学标注空节点及嵌入贴图。
Textures/：2048×2048 PNG，基础色、切线空间法线（+Y）、粗糙度、金属度四张独立 PBR 贴图；另附 Unity Metallic/Smoothness 和 HDRP Mask 两张打包图。
Editor/OcellarisUnitySetup.cs：Unity Editor 配置工具，可按当前渲染管线创建材质和 Prefab。
Ocellaris_UV_Layout_2K.png：实际 UV 线框图。
Validation_Report.json：Blender 源文件与 FBX 回导检查结果。
资源包的 Source/：可编辑、已内嵌贴图的 Blender 源文件，以及 GLB。
资源包的 Previews/：模型实物渲染；Reference/：生成式去水印修复的参考图。

Unity 导入
1. 解压资源包，将 Assets/Clownfish_Unity 整个目录放入项目的 Assets 目录，等待脚本编译完成。
2. 在 Project 窗口选中 Ocellaris_Unity.fbx。
3. 执行 Tools > Clownfish > Create Unity Prefab。
4. 将生成的 Ocellaris_Unity.prefab 拖入场景。
配置工具识别 Built-in Standard、URP Lit、HDRP Lit；会建立外部材质并连接贴图，使用 Generic 骨骼。
Source 目录无需放进 Assets。只想导入 FBX 时，也可直接使用 FBX 加 Textures 文件夹，并手动建立材质。
当前交付已在 Blender 4.5.3 做导出后回导验证，环境中没有 Unity 编辑器，Unity 脚本及各渲染管线效果尚未实机验证。

手动材质
BaseColor 接 Albedo/Base Map，按 sRGB 导入。
Normal 设为 Normal map，不翻转绿色通道；建议强度 0.30。
Roughness 以线性数据导入；Unity 通常使用 Smoothness = 1 - Roughness。
Builtin / URP：Ocellaris_UnityMetallicSmoothness_2K.png，R=Metallic=0，A=1-Roughness。
HDRP：Ocellaris_HDRPMask_2K.png，R=0，G=1，B=1，A=1-Roughness。
金属度为零；不使用发光、卡通描边或拟人化表情。预览使用摄影棚灯光，Unity 中的观感随灯光和色彩管理变化。

规格与拓扑
静止中性泳姿，鱼鳍略展开；源网格 19,147 顶点、19,148 面，98.50% 为四边面。
运行时三角形数约 38,008，FBX/Unity 会按需要三角化。
总长约 0.10037 米（含唇部），最大宽约 0.03143 米（含展开胸鳍），总高约 0.04628 米（含背/臀鳍）。
侧扁体干宽/高约 0.50；整体左右对称。
单一材质、单套无镜像叠用的 UV0。鳍两面与鳍边分别展开；4096 分辨率三角形内部覆盖检查重叠像素为零，无越界或退化 UV 三角形。
体干与鳍是独立、可命名的网格部件。眼、口、鳃盖边缘为独立表面；嵌入体内的唇/鳃盖线端点不封口。这是动画展示资产，不是供 3D 打印使用的封闭实体。

外观
依用户提供的照片重新配置较浓的橙到黄橙色、微带冷调的白带、较深的鳍缘与深色虹膜。
头后、体中、尾柄三条白带，边缘细黑；中带中央略向头侧凸出。
连续背鳍的前部表现 11 根硬棘，后部表现 15 根软条；臀鳍表现 2 硬棘与 12 软条；胸鳍纹理表现 17 条鳍条。
圆弧尾鳍、小端位口、成对胸鳍/腹鳍、可见眼与鳃盖、细密鳞片法线和半光泽表皮。
参考照片用于配色及材质细节，不将照片上的水印、白色背景或摄影照明投射进 UV 贴图。

骨骼（16）
Root；Spine_01 / 02 / 03；Tail_01 / 02；Caudal；Head；LowerJaw；
Dorsal_Spines；Dorsal_Soft；Anal；Pectoral_L / R；Pelvic_L / R。
全部网格具有规范化蒙皮权重，最大混合权重数不超过 3；骨骼已进行尾部、鳍和下颌变形检查。
未包含动画片段。可用 Generic rig 制作泳动。

课堂标注
Label_01_HeadBand：头后第一白带
Label_02_MidBand：身体中部第二白带
Label_03_PeduncleBand：尾柄第三白带
Label_04_BlackMargin：白带黑边
Label_05_CompressedBody：侧扁体干
Label_06_DorsalSpines：背鳍硬棘部
Label_07_DorsalSoftRays：背鳍软条部
Label_08_Pectoral_L / R：左右胸鳍
Label_09_Caudal：尾鳍
Label_10_Mouth：端位小口
Label_11_Eye_L / R：左右眼
Label_12_Operculum_L / R：左右鳃盖
Label_13_Anal：臀鳍
Label_14_Pelvic_L / R：左右腹鳍
这些空节点随整个模型变换移动，位置以静止姿态为准；未跟随局部骨骼蒙皮变形。做动画标注时，可将节点改挂对应骨骼或由脚本跟随表面。
L / R 用作建模侧别，Unity 导入后可按项目约定重命名。

参考资料
澳大利亚博物馆（物种特征、三白带、中带前凸、典型11背鳍硬棘及17胸鳍条）：
https://australian.museum/learn/animals/fishes/western-clown-anemonefish-amphiprion-ocellaris-cuvier-1830/
用户参考图（配色）：
https://pic.huitu.com/res/20260509/1426047_20260509062857683391_1.jpg
去水印参考图由内置 image_gen 工具进行生成式修复，部分遮挡细节为重建结果。
Unity 材质脚本 API / URP 源码参考：
https://docs.unity3d.com/ja/6000.0/ScriptReference/ModelImporter.SearchAndRemapMaterials.html
https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl
