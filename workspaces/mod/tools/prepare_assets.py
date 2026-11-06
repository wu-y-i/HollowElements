"""Stage accepted straight-alpha effects, supplied audio and a bitmap TMP font for Unity 2020.

No Unity 6 AssetBundle is shipped to the old game. Atlas coordinates use top-left origin,
as required by the game's embedded, pre-UPM version of TextMeshPro.
"""
import hashlib
import json
import pathlib
import shutil
import string
import wave
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "workspaces/art/source"
DEST = ROOT / "artifacts/mod/HollowElements/Assets"


def prepare():
    DEST.mkdir(parents=True, exist_ok=True)
    for folder, source, pattern in [
        ("Icons", SOURCE / "icons/elements", "*.png"),
        ("Audio", SOURCE / "audio/generated", "*.wav"),
    ]:
        target = DEST / folder
        target.mkdir(exist_ok=True)
        for path in source.glob(pattern):
            shutil.copy2(path, target / path.name)
    effect_source=ROOT / "artifacts/reaction-fx-v2" if (SOURCE/"reaction-fx-v2").exists() else ROOT / "artifacts/unity-fx"
    for manifest in effect_source.glob("*/manifest.json"):
        target = DEST / "Effects" / manifest.parent.name
        target.mkdir(parents=True, exist_ok=True)
        shutil.copy2(manifest, target / manifest.name)
        shutil.copy2(manifest.parent / "atlas.png", target / "atlas.png")
    font_dir = DEST / "Font"
    font_dir.mkdir(exist_ok=True)
    chars = set(string.printable[:95])
    for path in (ROOT / "workspaces/mod/src").rglob("*.cs"):
        if "obj" not in path.parts:
            chars.update(c for c in path.read_text(encoding="utf-8-sig") if not c.isspace())
    chars.update("蒸发融化超载感电冻结结霜超导碎冰无火水雷冰普通攻击波动砸地尖啸强力冲刺旋风斩关闭开启长椅配装返回应用保存重置重载测试木桩固定可击退自身附着受伤清除状态治疗灵魂参数调试模式剩余时间详情按键控制设置版本错误失败成功玩家敌人伤害元素反应（）【】·：，。！？←→↑↓＋−×％")
    font = ImageFont.truetype(str(SOURCE / "fonts/zh-cn.ttf"), 48)
    ascent, descent = font.getmetrics()
    image = Image.new("RGBA", (2048, 2048))
    draw = ImageDraw.Draw(image)
    x = y = 3
    row = 0
    glyphs = []
    for char in sorted(chars):
        if ord(char) < 32:
            continue
        left, top, right, bottom = font.getbbox(char, anchor="ls")
        padding = 2
        width, height = max(1, right-left)+padding*2, max(1, bottom-top)+padding*2
        if x + width + 3 > image.width:
            x = 3; y += row + 4; row = 0
        if y + height + 3 > image.height:
            raise RuntimeError("font atlas overflow")
        draw.text((x-left+padding, y-top+padding), char, font=font, anchor="ls", fill="white", stroke_width=0)
        glyphs.append(dict(id=ord(char), x=x, y=y, width=width, height=height, xOffset=left-padding, yOffset=-top+padding, xAdvance=font.getlength(char), scale=1))
        x += width + 4; row = max(row, height)
    image.save(font_dir / "atlas.png")
    outline = Image.new("RGBA", image.size, "white")
    alpha = image.getchannel("A")
    outline.putalpha(ImageChops.subtract(alpha.filter(ImageFilter.MaxFilter(5)), alpha))
    outline.save(font_dir / "outline.png")
    face = dict(Name="SDK_SC_Web", PointSize=48, Scale=1, CharacterCount=len(glyphs), LineHeight=ascent+descent,
                Baseline=0, Ascender=ascent, CapHeight=48, Descender=-descent, CenterLine=0, SuperscriptOffset=20,
                SubscriptOffset=-10, SubSize=.5, Underline=-4, UnderlineThickness=2, TabWidth=48,
                Padding=0, AtlasWidth=image.width, AtlasHeight=image.height)
    (font_dir / "glyphs.json").write_text(json.dumps(dict(face=face, glyphs=glyphs), ensure_ascii=False), encoding="utf-8")
    report = {}
    for path in sorted(DEST.rglob("*.wav")):
        with wave.open(str(path)) as audio:
            report[path.name] = dict(channels=audio.getnchannels(), rate=audio.getframerate(), seconds=audio.getnframes()/audio.getframerate(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (DEST.parent / "audio-validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"Staged {len(glyphs)} glyphs, {len(report)} audio clips, {len(list((DEST/'Effects').glob('*/manifest.json')))} effects")


if __name__ == "__main__":
    prepare()
