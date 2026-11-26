"""Генерация иконки приложения FlukeEventView.

Стиль повторяет корпус регистратора Fluke VR101: жёлтый прямоугольник,
верхняя треть которого чёрная (там расположено окно ИК-порта/дисплей).
"""
from PIL import Image, ImageDraw

SIZE = 1024
YELLOW = (240, 192, 0, 255)      # корпус регистратора
BLACK = (20, 20, 20, 255)        # верхняя треть корпуса
OUTLINE = (110, 86, 0, 255)      # тонкий контур для читаемости на светлом фоне
RADIUS = 104
MARGIN_X = 64
MARGIN_Y = 80
TOP_SHARE = 0.34                  # верхняя треть (слегка больше трети для читаемости)


def make_icon(size=SIZE):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    box = [MARGIN_X, MARGIN_Y, size - MARGIN_X, size - MARGIN_Y]
    d.rounded_rectangle(box, radius=RADIUS, fill=YELLOW)

    # Маска скруглённого корпуса, ограниченная сверху долей TOP_SHARE
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle(box, radius=RADIUS, fill=255)

    cut_y = int(MARGIN_Y + (box[3] - box[1]) * TOP_SHARE)
    top_mask = Image.new("L", (size, size), 0)
    top_mask.paste(body_mask.crop((0, 0, size, cut_y)), (0, 0))

    black_layer = Image.new("RGBA", (size, size), BLACK)
    img.paste(black_layer, (0, 0), top_mask)

    # Контур поверх, чтобы граница между чёрным и жёлтым была ровной
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box, radius=RADIUS, outline=OUTLINE, width=max(6, size // 128))
    d.line([(MARGIN_X, cut_y), (size - MARGIN_X, cut_y)], fill=OUTLINE, width=max(4, size // 192))

    return img


if __name__ == "__main__":
    import os
    import sys

    out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out_dir, exist_ok=True)

    master = make_icon()
    # Предпросмотр для документации
    master.resize((256, 256), Image.LANCZOS).save(os.path.join(out_dir, "icon_preview.png"))

    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
    ico_path = os.path.join(out_dir, "app.ico")
    master.save(ico_path, format="ICO", sizes=sizes)
    print("written:", ico_path, os.path.getsize(ico_path), "bytes")
    print("preview:", os.path.join(out_dir, "icon_preview.png"))
