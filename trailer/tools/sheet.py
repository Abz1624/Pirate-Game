"""Contact sheet of trailer takes: 4 frames per take, one row each, with name and duration."""
import subprocess, sys, os, glob, json
import imageio_ffmpeg
from PIL import Image, ImageDraw

FF = imageio_ffmpeg.get_ffmpeg_exe()

def duration(path):
    out = subprocess.run([FF, "-i", path], capture_output=True, text=True).stderr
    for line in out.splitlines():
        if "Duration" in line:
            h, m, s = line.split("Duration:")[1].split(",")[0].strip().split(":")
            return int(h) * 3600 + int(m) * 60 + float(s)
    return 0.0

def frame(path, t, w=320):
    data = subprocess.run([FF, "-v", "error", "-ss", f"{t:.2f}", "-i", path, "-frames:v", "1", "-vf", f"scale={w}:-1", "-f", "image2pipe", "-vcodec", "png", "-"], capture_output=True).stdout
    from io import BytesIO
    return Image.open(BytesIO(data)) if data else Image.new("RGB", (w, w * 9 // 16))

def main(pattern, out):
    files = sorted(glob.glob(pattern))
    W, H = 320, 180
    sheet = Image.new("RGB", (W * 4 + 220, H * len(files)), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    for r, f in enumerate(files):
        dur = duration(f)
        d.text((6, r * H + 6), f"{os.path.basename(f)}\n{dur:.1f}s", fill=(255, 255, 255))
        for c in range(4):
            im = frame(f, dur * (c + 0.5) / 4).resize((W, H))
            sheet.paste(im, (220 + c * W, r * H))
    sheet.save(out, quality=80)
    print(out, len(files))

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
