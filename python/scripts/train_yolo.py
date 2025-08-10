import argparse
from ultralytics import YOLO


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data', type=str, default='datasets/ult.yaml')
    parser.add_argument('--model', type=str, default='yolov8n.pt')
    parser.add_argument('--imgsz', type=int, default=192)
    parser.add_argument('--epochs', type=int, default=100)
    parser.add_argument('--batch', type=int, default=64)
    parser.add_argument('--project', type=str, default='runs')
    parser.add_argument('--name', type=str, default='detect/train')
    args = parser.parse_args()

    yolo = YOLO(args.model)
    yolo.train(
        data=args.data,
        imgsz=args.imgsz,
        epochs=args.epochs,
        batch=args.batch,
        project=args.project,
        name=args.name,
        device=0 if YOLO.is_cuda_available() else 'cpu',
        hsv_h=0.015,
        hsv_s=0.4,
        hsv_v=0.4,
        degrees=0.0,
        translate=0.05,
        scale=0.10,
        shear=0.0,
        mosaic=0.0,
        mixup=0.0,
    )


if __name__ == '__main__':
    main()


