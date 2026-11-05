"""Create the scene-owned Hangul font from the OFL Nanum Gothic original."""
from pathlib import Path
import sys
from fontTools.ttLib import TTFont

source, destination = map(Path, sys.argv[1:3])
font = TTFont(source)
for record in font['name'].names:
    if record.nameID in (1, 4, 6, 16):
        record.string = 'ParkingSigns'.encode(record.getEncoding(), errors='replace')
    elif record.nameID in (2, 17):
        record.string = 'Regular'.encode(record.getEncoding(), errors='replace')
font.save(destination)
required = '동네마트한빛부동산한길분식공영주차장이용안내입구출구요금정산주차금지장애인전용전기차충전구역경차서행'
missing = sorted(set(required) - set(map(chr, font.getBestCmap())))
if missing:
    raise RuntimeError('Missing Hangul glyphs: ' + ''.join(missing))
print('Korean scene font verified: all required characters present.')
