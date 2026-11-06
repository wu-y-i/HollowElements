"""Render exact, simple explanatory diagrams from the supplied element icons."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'workspaces/mod/content/ElementGuide/images'
FONT=ROOT/'workspaces/art/source/fonts/zh-cn.ttf';ICONS=ROOT/'workspaces/art/source/icons/elements'
def label(draw,xy,text,size=28,fill='#dce4eb',anchor='mm'):
 draw.text(xy,text,font=ImageFont.truetype(str(FONT),size),fill=fill,anchor=anchor)
def canvas():
 image=Image.new('RGBA',(1000,210),'#101822');d=ImageDraw.Draw(image);d.rounded_rectangle((2,2,998,208),18,outline='#455567',width=2);return image,d
def icon(image,name,x,y=72):
 source=Image.open(ICONS/(name+'.png')).convert('RGBA');source.thumbnail((78,78));image.alpha_composite(source,(int(x-source.width/2),int(y-source.height/2)))
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 image,d=canvas()
 for i,(name,title) in enumerate([('pyro','火'),('hydro','水'),('electro','雷'),('cryo','冰')]):
  x=200+i*200;icon(image,name,x,80);label(d,(x,150),title)
 image.save(OUT/'elements.png')
 for name,a,b,title,description in [('vaporize','pyro','hydro','蒸发','后手水：正向增幅    后手火：逆向增幅'),('melt','cryo','pyro','融化','后手火：正向增幅    后手冰：逆向增幅'),('overload','pyro','electro','超载','对敌：爆炸与击退    受击：后续伤害易伤'),('electro','hydro','electro','感电','对敌：持续电流    受击：4 / 8 / 12 秒扣血'),('freeze','hydro','cryo','冻结 / 结霜','能击退：冰块定身    不能击退：结霜减速')]:
  image,d=canvas();icon(image,a,150);label(d,(245,75),'+',40);icon(image,b,340);label(d,(445,75),'→',42);label(d,(700,75),title,36);label(d,(500,157),description,25);image.save(OUT/(name+'.png'))
 for name,steps,caption in [('superconduct',['冰 + 雷','3 层','2 层','1 层','结束'],'消耗一层后，剩余层数持续时间刷新'),('shatter',['冻结 / 结霜','无元素攻击 / 剑技','碎冰附伤'],'优先结算碎冰，清除普通附着与控制'),('hornet',['同元素 100%','50%','25%','换元素 / 反应','重置'],'图示为相对比例；交替元素可打破连续减伤'),('share',['驯兽师','共享附着','野兽'],'一方受到增幅 / 反应附伤，另一方受到实际伤害 × 1.5')]:
  image,d=canvas();width=940/len(steps)
  for i,step in enumerate(steps):
   x=30+width*(i+.5);d.rounded_rectangle((x-width*.43,40,x+width*.43,114),10,fill='#243746',outline='#617181');label(d,(x,77),step,23 if len(steps)<5 else 21)
   if i+1<len(steps):label(d,(x+width*.5,77),'→',24)
  label(d,(500,162),caption,24);image.save(OUT/(name+'.png'))
 print('Rendered 10 element_guide diagrams')
if __name__=='__main__':main()
