"""Pack PBR channels and author a metre-scaled road wear/paint mask for the Unity scene."""
from pathlib import Path
import json
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'unity/InputProbe/Assets/_Parkside/Lookdev'
TEX = ART / 'Textures'

for directory in TEX.iterdir():
    if not directory.is_dir():
        continue
    for arm in directory.glob('*arm*.jpg'):
        values = np.asarray(Image.open(arm).convert('RGB'))
        packed = np.zeros((*values.shape[:2], 4), dtype=np.uint8)
        packed[:, :, 0] = values[:, :, 2]
        packed[:, :, 1] = values[:, :, 0]
        packed[:, :, 3] = 255 - values[:, :, 1]
        Image.fromarray(packed).save(arm.with_name(arm.stem.replace('_arm_', '_metalSmooth_') + '.png'))
    for alpha in directory.glob('*alpha*'):
        diffuse = next(directory.glob(alpha.stem.replace('_alpha_', '_diff_') + '.jpg'), None)
        diffuse = diffuse or next(directory.glob(alpha.stem.replace('_alpha_', '_diff_') + '.png'), None)
        if diffuse:
            output = Image.open(diffuse).convert('RGBA')
            output.putalpha(Image.open(alpha).convert('L'))
            output.save(diffuse.with_suffix('.png'))

generated = ART / 'Generated'
generated.mkdir(parents=True, exist_ok=True)
concrete_image = np.asarray(Image.open(TEX / 'concrete_floor/concrete_floor_diff_2k.jpg').convert('RGB')).astype(float) / 255
grey = concrete_image.mean(axis=2)
for name, base, detail in [('FacadePlaster', .79, .13), ('CurbstoneColour', .64, .33)]:
    value = np.clip(base + (grey-grey.mean())*detail,0,1)
    output = np.stack([value,value*.995,value*.97],axis=2)
    Image.fromarray((output*255).astype('uint8')).save(generated / (name+'.png'))
SIZE = 2048
WORLD_X, WORLD_Z = 36, 32
rng = random.Random(2411)

def pixel(x, z):
    return ((x / WORLD_X + .5) * SIZE, (.5 - z / WORLD_Z) * SIZE)

def line(draw, points, width, fill):
    draw.line([pixel(x, z) for x, z in points], fill=fill, width=max(1, round(width / WORLD_X * SIZE)))

paint = Image.new('L', (SIZE, SIZE))
draw = ImageDraw.Draw(paint)
for index in range(9):
    x = -10.4 + index * 2.6
    line(draw, [(x, 3), (x, 8.4)], .1, 242)
line(draw, [(-10.4, 8.4), (10.4, 8.4)], .1, 242)
font_path = ROOT / 'unity/InputProbe/Assets/_Parkside/UI/Fonts/AstaSans-Bold.ttf'
font = ImageFont.truetype(str(font_path), 31)
for index in range(8):
    x, y = pixel(-9.1 + index * 2.6, 3.8)
    draw.text((x, y), str(101 + index), fill=210, font=font, anchor='mm')
# One simple circulation arrow, deliberately outside the parking stalls.
draw.polygon([pixel(x, z) for x, z in [(-.13,-5),(.13,-5),(.13,-3.5),(.6,-3.5),(0,-2.8),(-.6,-3.5),(-.13,-3.5)]], fill=225)
noise = np.random.default_rng(2411)
coarse = Image.fromarray(noise.integers(65, 250, (320, 320), dtype=np.uint8)).resize((SIZE, SIZE), Image.Resampling.BILINEAR)
wear = np.asarray(coarse) / 255
paint_values = np.asarray(paint).astype(float)
paint_values *= .55 + .45 * wear
speckle = noise.random((SIZE, SIZE))
paint_values[speckle < .055] *= .23

macro = Image.fromarray(noise.integers(65, 235, (24, 24), dtype=np.uint8)).resize((SIZE, SIZE), Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(23))
cracks = Image.new('L', (SIZE, SIZE))
draw = ImageDraw.Draw(cracks)
for cx, cz in [(-13,-5),(12,4),(-4,1.2),(5,10),(10,-11)]:
    points = [(cx, cz)]
    for _ in range(12):
        cx += rng.uniform(-.35,.5); cz += rng.uniform(.15,.5)
        points.append((cx,cz))
    line(draw, points, .018, 170)
    bx,bz = points[6]
    line(draw, [(bx,bz),(bx+.3,bz+.2),(bx+.7,bz+.35),(bx+1,bz+.3)], .012, 120)

oil = Image.new('L', (SIZE, SIZE))
draw = ImageDraw.Draw(oil)
for x,z in [(-8.9,6.1),(6.2,6.2),(-4.5,5.8),(3,-5)]:
    for _ in range(9):
        sx,sz = x+rng.uniform(-.6,.6),z+rng.uniform(-.5,.5)
        a,b = pixel(sx-.15,sz+.19),pixel(sx+.15,sz-.19)
        draw.ellipse([a,b], fill=rng.randint(20,75))
oil = oil.filter(ImageFilter.GaussianBlur(9))
packed = np.stack([np.asarray(macro), np.asarray(cracks), paint_values.clip(0,255).astype(np.uint8), np.asarray(oil)], axis=2)
Image.fromarray(packed).save(generated / 'ParkingWearMask.png')
print('Packed metallic/smoothness, leaf opacity, and 2048px road paint/wear maps.')
