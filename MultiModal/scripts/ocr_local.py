"""本地 PaddleOCR 引擎 —— Angri450.Nong.MultiModal 的子进程组件。
用法: python ocr_local.py --image <path> [--lang ch] [--gpu]
输出: JSON 到 stdout，格式 [[[bbox, (text, confidence)], ...]]
"""
import argparse
import json
import sys


def main():
    parser = argparse.ArgumentParser(description="PaddleOCR 本地文字识别")
    parser.add_argument("--image", required=True, help="图片路径")
    parser.add_argument("--lang", default="ch", help="OCR 语言 (默认 ch)")
    parser.add_argument("--gpu", action="store_true", help="使用 GPU")
    args = parser.parse_args()

    try:
        from paddleocr import PaddleOCR
    except ImportError:
        print(json.dumps({"error": "PaddleOCR 未安装，请运行: pip install paddlepaddle paddleocr"}))
        sys.exit(1)

    ocr = PaddleOCR(lang=args.lang, use_gpu=args.gpu)
    result = ocr.ocr(args.image)

    # 只保留 bbox + text + confidence
    simplified = []
    if result and result[0]:
        for line in result[0]:
            pts = line[0]
            text, confidence = line[1]
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            simplified.append({
                "bbox": [float(min(xs)), float(min(ys)), float(max(xs)), float(max(ys))],
                "text": text,
                "confidence": float(confidence),
            })

    print(json.dumps([simplified], ensure_ascii=False))


if __name__ == "__main__":
    main()
