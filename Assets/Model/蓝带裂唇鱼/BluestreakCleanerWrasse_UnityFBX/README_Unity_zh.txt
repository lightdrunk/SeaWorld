蓝带裂唇鱼成鱼 · Unity FBX 交付包

主文件：BluestreakCleanerWrasse_Adult_Unity.fbx
格式：FBX 7.4 二进制（FBX 7400）。已保留骨骼、蒙皮、UV、材质、标注节点及动画。
33个网格部件，34,708三角面，19根骨骼，17个 Label_* 标注节点。
模型全长约0.1135米；导出为Y轴向上的坐标约定，文件包含单位信息。
动画：Idle_Swim（3秒）与 Jaw_Demonstration（2秒），30fps采样，为两个独立FBX Take。

Unity导入方法
1. 将整个 BluestreakCleanerWrasse_UnityFBX 文件夹复制到项目 Assets 中。FBX可直接由Unity导入，无需glTF插件。
2. 等待资源和脚本导入后，在Project窗口选择主FBX文件，运行菜单 Tools > Wrasse > Set Up FBX Materials。
   随附 Editor/WrasseFbxMaterials.cs 会创建三种材质、接入贴图、设置Generic骨骼和循环动画。
   材质脚本支持 Built-in Standard 与 URP Lit。HDRP项目请按所用HDRP版本手动建立Lit材质与Mask Map。
3. 将FBX拖入场景；在Inspector的Rig中使用Generic，不使用Humanoid。
4. FBX有两段动画Take，菜单脚本会将动画片段命名为Idle_Swim和Jaw_Demonstration。播放片段时可按项目方式建立Animator Controller。
5. 保留默认文件单位转换与Scale Factor 1；检查模型全长约0.1135米。需放大教学展示时，缩放外层GameObject。

材质与贴图
FBX内嵌基础色和法线图用于基础交换；FBX传统材质通道不足以在所有引擎中自动恢复完整PBR效果，建议使用提供的独立2K贴图及菜单脚本。
Textures/Wrasse_BaseColor_2K.png：基础色，sRGB。
Textures/Wrasse_Normal_OpenGL_2K.png：切线法线，按Normal Map导入。
Textures/Wrasse_Roughness_2K.png：粗糙度，线性数据。
Textures/Wrasse_Metallic_2K.png：金属度，线性数据，全部为0。
Textures/Wrasse_Unity_MetallicSmoothness_2K.png：R为金属度，A为1减粗糙度；关闭sRGB，保留Alpha，接Metallic贴图槽，Smoothness倍率设1。
Textures/Wrasse_ORM_2K.png：R为1、G为粗糙度、B为金属度，供glTF或自定义Shader使用。

验证
使用Blender 4.5.13 LTS从原GLB导出，再在新空场景中重新导入FBX。
已核对：二进制头、33网格、34,708三角面、19骨骼、17标注节点、两个Take的名称与时长、模型尺寸。
两段动画在多个时间点的骨骼变形矩阵与源模型一致，最大数值差见 FBX_Verification.json。
本机没有Unity Editor，因此没有在Unity内执行导入或编译随附的编辑器脚本。
预览图展示原模型配色与结构；FBX沿用同一几何和贴图。

标注
Anatomy_Labels.json 的坐标仍为原GLB的右手坐标；Unity中请使用FBX内导入后的Label_*节点位置，不要直接套用JSON数值。
