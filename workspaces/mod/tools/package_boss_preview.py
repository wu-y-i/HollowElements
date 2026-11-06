"""Export the tested Boss 01–25 DLL without relabelling old acceptance as a new build."""
import datetime
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
from verify_release import verify

ROOT=Path(__file__).resolve().parents[3]
NAME="mod_v0.1.3-boss25"


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def write(path,value): path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding="utf-8")


def main():
    stage=ROOT/"artifacts/mod/HollowElements"
    evidence=ROOT/"artifacts/acceptance/boss-16-25"
    summary=read(evidence/"summary.json")
    assert summary["passed"] and summary["oldGuideUnchanged"]
    assert digest(stage/"HollowElements.dll")==summary["dllSha256"],"DLL differs from tested build"
    assert all(p.stat().st_mtime<=(stage/"HollowElements.dll").stat().st_mtime for p in (ROOT/"workspaces/mod/src").rglob("*.cs")),"Uncompiled source changes"
    assert not (stage/"development-session.json").exists(),"Development session enabled"
    reports=[]
    for group,folder in [(summary["cases"],evidence),(summary["regressions"],ROOT/"artifacts/acceptance/incoming-attacks")]:
        for item in group:
            path=folder/(item["case"]+".json")
            assert read(path)["passed"] and digest(path)==item["reportSha256"],path
            reports.append(dict(name=str(path.relative_to(ROOT/"artifacts/acceptance")).replace("\\","/"),sha256=digest(path)))
    release=ROOT/"artifacts/releases"/NAME
    archive=release.parent/(NAME+".zip")
    assert not release.exists() and not archive.exists(),"Existing releases are immutable"
    release.mkdir(parents=True)
    mod=release/"HollowElements";mod.mkdir()
    for filename in ["HollowElements.dll","HollowElements.pdb"]:
        shutil.copy2(stage/filename,mod/filename)
    shutil.copytree(stage/"Assets",mod/"Assets")
    shutil.copy2(ROOT/"workspaces/mod/balance.defaults.json",mod/"balance.defaults.json")
    instructions=(ROOT/"docs/mod_v0.1使用说明.md").read_text(encoding="utf-8")
    instructions=instructions.replace("# HollowElements v0.1.3 使用说明","# HollowElements Boss 01–25 预览版使用说明",1)
    instructions=instructions.replace("适用于 HollowElements 0.1.3（v0.1 界面修订版）。","本包为 Boss 01–25 设计分支快照，沿用已验收 DLL，Mod 菜单中的版本号仍为 0.1.3。详细范围见随包《本版改动》。",1)
    a=instructions.index("## 首版范围")
    b=instructions.index("## 开发入口",a)
    instructions=instructions[:a]+"""## 当前范围

包含四元素反应、长椅配装、调试木桩、血条、跳字、特效与音效，以及五门列表 01–25 已设计的专属机制和已实现的关联版本。吸虫之母本体保持原版，召唤幼雏按设计修改。具体以随包《本版改动》为准。

所有水池／温泉进入时施加 1U 水附着，停留每秒刷新；酸液排开后，酸液不再施加水附着。玩家攻击使用各来源独立的计时／计数规则，敌方每次实际有效受伤独立附着。

后续 Boss 尚未设计实现的专属机制不在本包范围内；它们仍使用通用元素反应。岩图标仅保留素材，没有结晶反应或新护符。血条显示当前实体／阶段，不合成多阶段累计血量。

原版辐辉的一击致死继续生效。冻结时的法术降级不改永久解锁等级。冻结与结霜使用局部时钟，不修改全局游戏速度。

"""+instructions[b:]
    (release/"安装与使用.md").write_text(instructions,encoding="utf-8")
    shutil.copy2(ROOT/"docs/Boss01-25预览版说明.md",release/"本版改动.md")
    guides=release/"开发指南";guides.mkdir()
    for name in ["Boss机制开发与常用API指南.md","Boss05-10实现思路.md","Boss11-15实现思路.md","Boss16-25实现思路.md"]:
        shutil.copy2(ROOT/"docs"/name,guides/name)
    for p in release.rglob("*"):
        if p.is_file():
            assert p.name not in ["development-session.json","Assembly-CSharp.dll","UnityPlayer.dll"]
            assert not p.name.startswith("user")
    metadata=dict(version="0.1.3",releaseName=NAME,label="Boss 01–25 预览版",created=datetime.datetime.now().astimezone().isoformat(),game="1.5.78.11833",api=77,runtime="net472",language="zh-CN",effects=10,frames=249,audio=7,icons=5,debugDefault=False,dllSha256=summary["dllSha256"],reports=reports,
        acceptance=dict(ruleChecks=summary["ruleChecks"],mechanismChecks=summary["mechanismChecks"],newPhysicalContacts=summary["newPhysicalContacts"],regressionPhysicalContacts=summary["regressionPhysicalContacts"],scope=summary["scope"],summarySha256=digest(evidence/"summary.json")))
    write(release/"verification.json",metadata)
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=digest(p)) for p in sorted(release.rglob("*")) if p.is_file()]
    write(release/"manifest.json",dict(version="0.1.3",files=files))
    with zipfile.ZipFile(archive,"x",compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(release.rglob("*")):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    report=verify(archive)
    write(ROOT/"artifacts/acceptance/boss-16-25/package-verification.json",dict(**report,releaseName=NAME))
    (archive.parent/(archive.name+".sha256")).write_text(digest(archive)+"  "+archive.name+"\n",encoding="ascii")
    print(json.dumps(dict(path=str(archive),bytes=archive.stat().st_size,**report),ensure_ascii=False,indent=2))


if __name__=="__main__": main()
