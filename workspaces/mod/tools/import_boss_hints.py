"""Read the four authoring columns from XLSX and export validated runtime JSON."""
import argparse,json,re,zipfile,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];NS={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def read_rows(path):
 with zipfile.ZipFile(path) as z:
  shared=[]
  if 'xl/sharedStrings.xml' in z.namelist():shared=[''.join(x.itertext()) for x in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si',NS)]
  workbook=ET.fromstring(z.read('xl/workbook.xml'));sheet=workbook.find('s:sheets/s:sheet',NS);rid=sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']
  rels=ET.fromstring(z.read('xl/_rels/workbook.xml.rels'));target=next(r.attrib['Target'] for r in rels if r.attrib['Id']==rid);target=target.lstrip('/') if target.startswith('/') else 'xl/'+target
  root=ET.fromstring(z.read(target));rows=[]
  for row in root.findall('s:sheetData/s:row',NS):
   values=['']*4
   for cell in row.findall('s:c',NS):
    col=re.match('[A-Z]+',cell.attrib['r']).group();index=0
    for ch in col:index=index*26+ord(ch)-64
    if index>4:continue
    v=cell.find('s:v',NS);value=v.text if v is not None else ''
    if cell.attrib.get('t')=='s':value=shared[int(value)]
    elif cell.attrib.get('t')=='inlineStr':value=''.join(cell.find('s:is',NS).itertext())
    values[index-1]=value or ''
   rows.append((int(row.attrib['r']),values))
  return rows

def convert(path):
 index=json.loads((ROOT/'workspaces/mod/content/ElementGuide/index.json').read_text(encoding='utf-8-sig'));known={e['Id']:e for c in index['Categories'] for e in c['Entries'] if e['Id'].startswith('boss-')};entries=[];seen=set();header=False
 for n,values in read_rows(path):
  if values==['Boss ID','Boss名','说明','是否显示']:header=True;continue
  if not header or not any(values):continue
  id,name,text,enabled=[str(x).strip() for x in values]
  if id not in known or id in seen:raise ValueError(f'第{n}行 Boss ID 无效或重复：{id}')
  seen.add(id);text=' '.join(text.split())
  if enabled.lower() not in ['是','否','true','false','1','0','yes','no']:raise ValueError(f'第{n}行是否显示应选“是”或“否”')
  show=enabled.lower() in ['是','true','1','yes']
  if len(text)>60 or '<' in text or '>' in text or show and not text:raise ValueError(f'第{n}行说明为空、超过60字或含富文本标记')
  entries.append(dict(Id=id,Name=name or known[id]['Title'],Text=text,Enabled=show,Scenes=known[id]['Scenes']))
 if not header or not entries:raise ValueError('未找到 Boss 提示配置表')
 return dict(Version=1,Entries=entries)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('xlsx',type=Path);p.add_argument('--output',type=Path,default=ROOT/'workspaces/mod/content/BossHints.json');a=p.parse_args();config=convert(a.xlsx);a.output.parent.mkdir(parents=True,exist_ok=True)
 tmp=a.output.with_suffix('.json.tmp');tmp.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(a.output);print('Exported',len(config['Entries']),'bosses;',sum(e['Enabled'] for e in config['Entries']),'enabled')
