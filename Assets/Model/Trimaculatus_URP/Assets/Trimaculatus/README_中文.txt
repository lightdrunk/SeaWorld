三斑海马 · Unity URP 教学模型
Hippocampus trimaculatus · 雄性 · 直立中性姿态

一、导入 Unity

目标工作流：Unity 2022.3 LTS / URP 14，或 Unity 6 / URP。
本机未安装 Unity Editor，因此未执行 Unity 内的编译、播放或真机测试。
已使用 Blender 4.5.3 对导出的实际 FBX 执行独立回读检查；具体结果见
Documentation/ValidationReport.json。

1. 在已经启用 URP 的项目中导入 Trimaculatus_URP.unitypackage；
   或将本目录内的 Assets/Trimaculatus 文件夹复制到项目的 Assets 下。
   两种方法任选一种，无需重复导入。
2. 等待脚本编译，然后执行：
   Tools > Seahorse > Build URP Teaching Prefab
3. 将生成的 Assets/Trimaculatus/Generated/Hippocampus_trimaculatus_URP.prefab
   拖入场景。

配置工具会生成 URP/Lit 材质、Generic 动画控制器和预制体，保留骨骼与锚点。
它不会修改项目的渲染管线或当前场景。URP 必须由项目预先启用。
若仅导入 FBX，Unity 不会自动把 Blender 材质转换成 URP/Lit，请使用上述工具。

真实尺寸：模型直立投影高度约 0.1496 米；1 Unity unit = 1 米。
导入缩放为 1，保留文件单位。展示放大请缩放最外层 GameObject。
近距离查看时可把相机 Near Clip Plane 调为 0.005 米；正交相机 Size 约 0.09 米。
模型在 Unity 中 Y 向上，头部朝模型局部 -X，左右位于 Z 两侧。

二、模型内容

33 个独立命名网格，25,100 个源顶点，49,824 个三角面。
单一共享 PBR 材质图集，原始几何以四边面为主，导入时由引擎三角化。
主骨甲壳从管状吻经弯颈、躯干连续连接到尾尖；其余解剖部件为独立封闭网格。
左右几何对称，图集左右不镜像堆叠。

固定采用的计数：
躯干环 11；尾环 41；极低骨冠的微小突起 5；背鳍软条 20；
每侧胸鳍软条 18；臀鳍软条 4；背鳍硬棘 0；腹鳍 0；尾鳍 0。
躯干和尾部为骨甲环/骨片及隆起连接脊，没有覆瓦状鳞片。
吻长/头长的目标比例约 1/2.2。
颊部和眼上钩状棘分别命名。没有鼻棘。
单一背鳍跨末两躯干环与首尾环附近；臀鳍位于肛门后方，极小。
雄性育儿囊位于近端尾部腹侧。长尾在矢状面内自然向腹侧弯曲，
远离躯干过渡区后横截面为圆角四棱形。

配色采用沙金色型，第一、第四、第七躯干环的背侧各有一对对应暗斑。
这是原创程序化教学重建，不是 CT、标本扫描或个体形态测量数据。

三、2K PBR 贴图

所有 PNG 均为 2048 × 2048。
HT_BaseColor_2K.png：基础色，sRGB 开启。
HT_Normal_OpenGL_2K.png：切线空间 +Y 法线，Unity Texture Type = Normal map。
HT_Roughness_2K.png：独立粗糙度，线性数据。
HT_Metallic_2K.png：独立金属度，全黑/0，线性数据。
HT_URP_MetallicSmoothness_2K.png：Unity URP 专用打包图，
R = Metallic = 0，A = Smoothness = 1 - Roughness，G/B = 0，sRGB 关闭。

URP/Lit：Base Map 接基础色；Normal Map 接法线；Metallic Map 接打包图；
Smoothness Source = Metallic Alpha；Smoothness = 1；Normal Strength = 0.6。
粗糙度不能直接连接到 URP 的 Smoothness。透明度不参与该材质，
鳍采用有厚度、双面有各自 UV 的浅色实体薄膜，适合观察与标注。
模型没有金属表面。贴图数据和纹理导入默认不压缩，以保留教学细节。

四、绑定和动画

65 根骨骼：Root、11 段躯干链、41 段尾链、Head、LowerJaw、
独立 Eye.L / Eye.R、Pectoral.L / Pectoral.R、5 根 DorsalWave、AnalFin。
全部顶点已归一化蒙皮，最多 2 根骨骼影响。
绑定为 Generic，适用于非人形动物；不使用 Humanoid Avatar。
自然卷尾是绑定静止姿态。进一步卷曲可操作 Tail_01 至 Tail_41；
编辑时在该弯曲静止姿态上施加局部旋转。

FBX 内有 3 个动画 Take：
Idle_FinPropulsion_2s：2 秒循环，背鳍波动与胸鳍拍动为主；双眼独立微转。
TailCurl_Demonstration_3s：3 秒尾部进一步卷曲再回到静止姿态。
Jaw_SubtleOpen_1s：1 秒下颌小幅开合。
演示动画不包含真实流体求解或位移轨迹。

生成控制器状态：IdleFinPropulsion（默认）、TailCurl、JawOpen。
可使用 Animator.Play("TailCurl", 0, 0) 或 Animator.Play("JawOpen", 0, 0) 演示；
演示结束后调用 Animator.Play("IdleFinPropulsion", 0, 0) 返回游动。
Root Motion 关闭。没有尾鳍推进动作。

五、教学标注

FBX 保留 70 个以 Label_ 开头、跟随对应骨骼的空节点：
11 个 TrunkRing_01...11、41 个 TailRing_01...41，以及 18 个关键结构节点。
完整中文名和骨骼对应关系在 Assets/Trimaculatus/AnatomyManifest.json。
前缀 Label_ 后的字符串即 anatomy ID。
例如：TubularSnout、LowCoronet_5TinyPoints、DorsalFin_20Soft、MaleBroodPouch。

生成预制体上的 TrimaculatusAnnotations 组件可通过
FindAnchor("TubularSnout") 获取 Transform，用于世界空间 UI 或自定义标注。
启用 showLabels 可显示简单中文标签；focusId 可只显示一个结构，
includeIndividualRings 可显示单环标签。默认关闭叠加标签以避免画面拥挤。
如项目默认字体缺少中文字形，请在 labelFont 指定含中文的 Font。
NoPelvicFins 和 TailTip_NoCaudalFin 用于标注缺失结构，不对应实际鳍网格。
在 Blender 源文件中，主壳 POINT 整数属性 trunk_ring_id / tail_ring_id
还记录了各环的顶点归属；FBX 中以逐环锚点供 Unity 使用。

六、其他文件

Hippocampus_trimaculatus_Rigged.glb：辅助交换文件，自带 PBR 材质和动画。
Hippocampus_trimaculatus_Source.blend：含绑定、材质和检查渲染场景的可编辑源文件。
Preview_*.png：从实际模型渲染的预览，不是照片或生成式概念图。
SourceScripts/：生成与验证脚本，Unity 使用时无需运行。
Documentation/ValidationReport.json：源模型和 FBX 回读的机器检查结果。

解剖参考：
Lourie et al. (2004), A Guide to the Identification of Seahorses，物种页 80。
https://projectseahorse.org/wp-content/uploads/2021/06/Seahorse_ID_Guide_2004.pdf
ICAR-CMFRI，Hippocampus trimaculatus Leach, 1814：
https://eprints.cmfri.org.in/14956/
参考资料仅用于形态核对；模型和贴图为本次创建，未将指南插图嵌入贴图。

URP 打包通道参考 Unity 官方文档：
https://docs.unity.cn/Packages/com.unity.render-pipelines.universal@17.0/manual/lit-shader.html
