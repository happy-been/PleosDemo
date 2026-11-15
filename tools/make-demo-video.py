"""Joins the demo-video frames (Unity editor, DemoVideo) into the submission MP4.

    python tools/make-demo-video.py [--stills DIR] [--out FILE]

build/demo-video/frames/00000.jpg ... are the recorded frames at 30 fps. result.txt says at which frame the ending
title starts ("ending@N"); a short montage of landmark stills (slow zoom, caption) is cut in there.
Needs ffmpeg on PATH and Pillow.
"""
import argparse
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DEMO = ROOT / 'build' / 'demo-video'
FONT = ROOT / 'unity' / 'InputProbe' / 'Assets' / '_Parkside' / 'UI' / 'Fonts' / 'AstaSans-Medium.ttf'
FPS = 30
STILLS = [  # (file name in the stills folder, caption)
    ('2_부산/2_부산_08_광안대교_전경_망원.jpg', '부산 · 광안대교'),
    ('4_파리/4_파리_20_에펠탑_전경_2.jpg', '파리 · 에펠탑'),
    ('4_파리/4_파리_21_개선문_전경_1.jpg', '파리 · 개선문'),
    ('3_제주/3_제주_01_예래_해안_드론.jpg', '제주 · 해안도로'),
]


def run(args):
    print(' '.join(str(a) for a in args[:6]), '...')
    subprocess.run([str(a) for a in args], check=True)


def caption_still(source, text, target):
    image = Image.open(source).convert('RGB').resize((1920, 1080), Image.LANCZOS)
    draw = ImageDraw.Draw(image, 'RGBA')
    font = ImageFont.truetype(str(FONT), 34)
    width = draw.textlength(text, font=font)
    box = (960 - width / 2 - 34, 1080 - 150, 960 + width / 2 + 34, 1080 - 82)
    draw.rounded_rectangle(box, radius=22, fill=(12, 15, 18, 215))
    draw.text((960 - width / 2, 1080 - 141), text, font=font, fill=(244, 246, 248))
    image.save(target, quality=95)


def encode(pattern_dir, start, count, out, fade_in=False, fade_out=False):
    filters = []
    if fade_in: filters.append('fade=t=in:st=0:d=0.5')
    if fade_out: filters.append(f'fade=t=out:st={count / FPS - 0.6:.2f}:d=0.6')
    vf = ','.join(filters + ['format=yuv420p']) if filters else 'format=yuv420p'
    run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', FPS, '-start_number', start, '-i', pattern_dir / '%05d.jpg',
         '-frames:v', count, '-vf', vf, '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-r', FPS, out])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stills', default=str(Path.home() / 'Desktop' / '공모전' / '01_진행중' / 'HMG_차량용앱개발공모전_1115' / '02_제출물' / '제출사진_1115'))
    parser.add_argument('--out', default=str(DEMO / 'parking-travel-demo.mp4'))
    parser.add_argument('--cut', action='append', default=[], help='frames to leave out, START:END (END excluded), e.g. 2508:2778')
    args = parser.parse_args()
    frames = DEMO / 'frames'
    total = len(list(frames.glob('*.jpg')))
    result = (DEMO / 'result.txt').read_text(encoding='utf-8')
    match = re.search(r'ending@(\d+)', result)
    ending = int(match.group(1)) if match else total
    work = Path(tempfile.mkdtemp(prefix='demo-'))
    try:
        parts = []
        # the recorded part before the ending, without the cut frame ranges
        cuts = sorted(tuple(int(v) for v in c.split(':')) for c in args.cut)
        keep, at = [], 0
        for start, end in cuts:
            if start > at: keep.append((at, min(start, ending)))
            at = max(at, end)
        if at < ending: keep.append((at, ending))
        for i, (start, end) in enumerate(keep):
            clip = work / f'a{i}.mp4'
            encode(frames, start, end - start, clip, fade_in=i == 0)
            parts.append(clip)
        for i, (name, text) in enumerate(STILLS):
            source = Path(args.stills) / name
            if not source.exists(): print('missing still', source); continue
            still = work / f'still{i}.jpg'
            caption_still(source, text, still)
            clip = work / f'still{i}.mp4'
            seconds = 2.4
            run(['ffmpeg', '-y', '-loglevel', 'error', '-loop', '1', '-i', still, '-t', seconds,
                 '-vf', f"scale=2400:-1,zoompan=z='min(zoom+0.0007,1.05)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={int(seconds * FPS)}:s=1920x1080:fps={FPS},"
                        f"fade=t=in:st=0:d=0.3,fade=t=out:st={seconds - 0.3}:d=0.3,format=yuv420p",
                 '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-r', FPS, clip])
            parts.append(clip)
        if total > ending:
            encode(frames, ending, total - ending, work / 'b.mp4', fade_in=True, fade_out=True)
            parts.append(work / 'b.mp4')
        listing = work / 'list.txt'
        listing.write_text(''.join(f"file '{p.as_posix()}'\n" for p in parts), encoding='utf-8')
        run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', listing, '-c', 'copy', '-movflags', '+faststart', args.out])
        print('video', args.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    main()
