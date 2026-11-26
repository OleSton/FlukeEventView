"""Генерация тестового файла .VER «sweep»: по одной записи на каждый код B1 (0x00..0xFF).

Назначение: открыть файл в оригинальном EventView и выгрузить CSV/список, чтобы получить
эталонную таблицу «код события -> как его называет оригинал». Особенно интересен код,
соответствующий сообщению «%1 Loss of Synchronization».

Заголовок берётся из поставлявшегося с программой SAMPLE.VER (валидный заголовок Fluke),
массив событий заменяется нашим.
"""
import struct
import sys

TEMPLATE = r"C:\EV101\SAMPLE.VER"
HEADER_LEN = 152
RECORD_LEN = 16

def main(out_path):
    with open(TEMPLATE, "rb") as f:
        data = bytearray(f.read())

    header = data[:HEADER_LEN]

    events = bytearray()
    for code in range(256):
        b = bytearray(8)
        b[0] = code          # B1 — код события
        b[1] = 100           # B2 — амплитуда (200 В для всплеска, 1000 Вп для импульса)
        b[2] = 0x00          # B3..B5 — длительность/фаза
        b[3] = 0x00
        b[4] = 0x10
        tick = code * 2      # B6..B8 — метка времени (тики по 8 с)
        b[5] = (tick >> 16) & 0xFF
        b[6] = (tick >> 8) & 0xFF
        b[7] = tick & 0xFF

        # Каждая запись в файле — 8 значений UInt16 (little-endian), значим младший байт
        for value in b:
            events += struct.pack("<H", value)

    count = len(events) // RECORD_LEN
    header[110:114] = struct.pack("<i", count * 8)
    header[114:118] = struct.pack("<i", count * 8)
    header[150:152] = struct.pack("<H", 1)

    with open(out_path, "wb") as f:
        f.write(header)
        f.write(events)

    print(f"written: {out_path}")
    print(f"events: {count}, size: {HEADER_LEN + len(events)} bytes")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "codesweep.ver")
