Ultralytics 训练与导出（Windows）

目录结构（可按需调整）

python/
- datasets/
  - ult.yaml            # YOLO 数据集配置（2 类：open/close）
  - images/train/       # 训练图片（ROI 小图）
  - images/val/         # 验证图片
  - labels/train/       # YOLO 标注 txt（同名，同尺寸）
  - labels/val/
- scripts/
  - train_yolo.py       # 训练脚本
  - export_onnx.py      # 导出 ONNX（可 DirectML 推理）
- requirements.txt

快速开始

1) 创建虚拟环境并安装依赖
   PowerShell:
   cd src/UltAssist/python
   python -m venv .venv
   .\\.venv\\Scripts\\Activate
   pip install -r requirements.txt

2) 准备数据
   - 将 ROI 小图放入 datasets/images/train 与 datasets/images/val
   - 使用 YOLO 标注工具（如 labelImg/Roboflow/Ultralytics Label Studio）标 2 类：open、close
   - 标注文件放在 datasets/labels/*，与图像同名（.txt）

3) 训练（默认使用 ultralytics 官方 nano 权重）
   python scripts/train_yolo.py --data datasets/ult.yaml --epochs 100 --imgsz 192 --batch 64 --model yolov8n.pt

4) 导出 ONNX（用于 WPF 内 DirectML 推理）
   python scripts/export_onnx.py --weights runs/detect/train/weights/best.pt --imgsz 192 --half --simplify

产物位置
- 训练输出：python/runs/detect/train/
- 导出模型：python/exports/best.onnx（可改路径）

提示
- 首选 GPU 推理（WPF 侧使用 ONNX Runtime DirectML）；无 GPU 时自动回退 CPU。
- ROI 分辨率建议 160~224；训练与推理 imgsz 保持一致更稳。


