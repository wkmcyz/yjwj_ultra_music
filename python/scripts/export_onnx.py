import argparse
from ultralytics import YOLO
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--weights', type=str, required=True, help='path to .pt')
    parser.add_argument('--imgsz', type=int, default=192)
    parser.add_argument('--half', action='store_true')
    parser.add_argument('--simplify', action='store_true')
    parser.add_argument('--out', type=str, default='exports/best.onnx')
    args = parser.parse_args()

    model = YOLO(args.weights)
    Path('exports').mkdir(parents=True, exist_ok=True)
    model.export(format='onnx', imgsz=args.imgsz, half=args.half, simplify=args.simplify, opset=12)


if __name__ == '__main__':
    main()


