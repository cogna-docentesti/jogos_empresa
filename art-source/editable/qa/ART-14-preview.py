"""ART-14 native/display/identity reviews and a composed identification proof only."""
from pathlib import Path
import json,hashlib,io,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw,ImageFont,ImageCms
import numpy as np
R=Path(__file__).resolve().parents[3];P=R/'art-source/previews/ART-14';P.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def rel(p):return p.relative_to(R).as_posix()
def image(path):return Image.open(R/path).convert('RGBA')
def bg(im,color):
 out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')
def contain(im,size):
 s=min(size[0]/im.width,size[1]/im.height);return im.resize((round(im.width*s),round(im.height*s)),Image.Resampling.LANCZOS)
def sliced(im,size,border):
 w,h=im.size;dw,dh=size;xs=[0,border,w-border,w];ys=[0,border,h-border,h];dx=[0,border,dw-border,dw];dy=[0,border,dh-border,dh];out=Image.new('RGBA',size)
 for y in range(3):
  for x in range(3):
   tile=im.crop((xs[x],ys[y],xs[x+1],ys[y+1])).resize((dx[x+1]-dx[x],dy[y+1]-dy[y]),Image.Resampling.BILINEAR);out.paste(tile,(dx[x],dy[y]))
 return out
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-14.json');records=prod['assets'];lookup={r['id']:r for r in records};imgs={r['id']:image(r['output_path']) for r in records};results=[];previews=[]
for r in records:
 im=Image.open(R/r['output_path']);alpha=im.getchannel('A');a=np.asarray(alpha);profile=ImageCms.getProfileName(ImageCms.ImageCmsProfile(io.BytesIO(im.info['icc_profile']))).strip()
 result={'id':r['id'],'path':r['output_path'],'actual_size_px':list(im.size),'bytes':(R/r['output_path']).stat().st_size,'sha256':sha(r['output_path']),
  'rgba':im.mode=='RGBA','srgb_profile':profile,'alpha_extrema':list(alpha.getextrema()),'alpha_bbox_nonzero_px':alpha.getbbox(),
  'exact_manifest_dimensions':list(im.size)==next(a['export_size_px'] for a in m['assets'] if a['id']==r['id']),
  'real_zero_alpha':bool(np.any(a==0)),'hash_matches_production':r['sha256']==sha(r['output_path'])}
 if r['id']=='ID_ENVIRONMENT':result['fully_opaque']=bool(np.all(a==255))
 else:result['transparent_edges']=bool(np.all(a[0,:]==0) and np.all(a[-1,:]==0) and np.all(a[:,0]==0) and np.all(a[:,-1]==0))
 if r['production_method']=='vector_or_code':
  xml=ET.parse(R/r['source_editable']).getroot();tags={el.tag.split('}')[-1] for el in xml.iter()};result['no_embedded_text_image_or_effect']=not bool(tags & {'text','image','filter','foreignObject'})
 if r['id']=='CH01_WELCOME':
  x,y,w,h=r['visible_bbox_alpha_128_px'];result['vertical_visible_margins_fraction']=[y/im.height,(im.height-y-h)/im.height]
  result['visible_vertical_padding_at_least_10percent']=min(result['vertical_visible_margins_fraction'])>=.1
  identity=next(c for c in read('docs/art/characters.json') if c['id']=='CH01');result['canonical_master_hash_preserved']=sha(identity['canonical_reference_path'])==identity['identity_hash']==r['canonical_reference']['sha256']
 if r['id']!='ID_ENVIRONMENT':
  native=Image.new('RGB',(im.width*2+16,im.height+36),'#F5F0E8');d=ImageDraw.Draw(native);d.text((8,6),r['id']+' | native',font=font,fill='#24113D')
  for i,color in enumerate(['#F5F0E8','#24113D']):native.paste(bg(im,color),(i*(im.width+16),36))
  npth=P/(r['id']+'-native-light-dark.png');native.save(npth);previews.append(rel(npth));result['native_preview']=rel(npth)
  bw,bh=r['display_size_px'];display=Image.new('RGB',(bw*2+16,bh+36),'#F5F0E8');d=ImageDraw.Draw(display);d.text((8,6),r['id']+' | contain '+str(bw)+'x'+str(bh),font=font,fill='#24113D');fitted=contain(im,(bw,bh))
  for i,color in enumerate(['#F5F0E8','#24113D']):
   origin=(i*(bw+16),36);d.rectangle((origin[0],origin[1],origin[0]+bw-1,origin[1]+bh-1),fill=color)
   display.paste(bg(fitted,color),(origin[0]+(bw-fitted.width)//2,origin[1]+(bh-fitted.height)//2))
  dp=P/(r['id']+'-display-light-dark.png');display.save(dp);previews.append(rel(dp));result['display_preview']=rel(dp);result['whole_texture_contain_size_px']=list(fitted.size)
 # Context-specific alpha: the full background is opaque; three independent cutouts require zero outside.
 checks=[v for k,v in result.items() if isinstance(v,bool) and k!='real_zero_alpha'];result['passed']=all(checks) and (r['id']=='ID_ENVIRONMENT' or result['real_zero_alpha']) and 'srgb' in profile.lower();results.append(result)
left=np.asarray(imgs['ID_ORNAMENT_LEFT']);right=np.asarray(imgs['ID_ORNAMENT_RIGHT']);delta=np.abs(left[:,:,3][:,::-1].astype(int)-right[:,:,3].astype(int))
left_svg=ET.parse(R/lookup['ID_ORNAMENT_LEFT']['source_editable']).getroot();right_svg=ET.parse(R/lookup['ID_ORNAMENT_RIGHT']['source_editable']).getroot()
lg=list(left_svg)[0];rg=list(right_svg)[0];vector_mirror=rg.attrib.get('transform')=='translate(512 0) scale(-1 1)' and [ET.tostring(el) for el in lg]==[ET.tostring(el) for el in rg]
mirror_equal=bool(vector_mirror and delta.max()<=8 and delta.mean()<.01);assert mirror_equal
# Face/hair and hands are reviewed visually at comfortable size; canonical identity is never redefined.
canonical=image('Assets/Art/new/characters/CH01/ch01_canonical.png');welcome=imgs['CH01_WELCOME']
identity=Image.new('RGB',(900,540),'#F5F0E8');d=ImageDraw.Draw(identity)
for i,(label,im,box) in enumerate([('CH01 master',canonical,(370,215,640,445)),('CH01 welcome',welcome,(352,177,627,430))]):
 crop=im.crop(box);crop=contain(crop,(400,375));identity.paste(bg(crop,'#F5F0E8'),(i*450+25,50));d.text((i*450+25,15),label,font=font,fill='#24113D')
identity_path=P/'CH01-identity-comparison.png';identity.save(identity_path);previews.append(rel(identity_path))
hands=welcome.crop((310,440,872,656));hands_path=P/'CH01-welcome-hands-clipboard.png';bg(hands,'#F5F0E8').save(hands_path);previews.append(rel(hands_path))
# Proposed layer layout, separate from game scenes. Person is placed by visible bounds for this full composition.
layers=[{'asset_id':'ID_ENVIRONMENT','position_px':[0,0],'draw_size_px':[1920,1080],'uniform_scale':1,'order':0},
 {'asset_id':'ID_ORNAMENT_LEFT','position_px':[0,270],'draw_size_px':[320,640],'uniform_scale':.625,'contain_box_px':[0,160,320,860],'order':10},
 {'asset_id':'ID_ORNAMENT_RIGHT','position_px':[1580,270],'draw_size_px':[320,640],'uniform_scale':.625,'contain_box_px':[1580,160,320,860],'order':10},
 {'asset_id':'CH01_WELCOME','position_px':[15,57],'draw_size_px':[654,981],'uniform_scale':654/1024,'visible_target_box_px':[170,174,420,760],
  'alignment_note':'Apenas nesta montagem, encaixe uniforme pela silhueta visível; o preview isolado usa contain da textura inteira na caixa nominal 420x760. Nenhum alfa foi removido.',
  'visible_source_bbox_px':lookup['CH01_WELCOME']['visible_bbox_alpha_128_px'],'order':20}]
layout={'schema_version':1,'task_id':'ART-14','date':'2026-10-14','canvas_px':[1920,1080],'coordinate_origin':'top_left_y_down',
 'coordinate_semantics':'position_px é top-left da textura desenhada; escala uniforme. Não é anchoredPosition de RectTransform com pivot central.',
 'form_reserved_rect_px':[1180,184,650,772],'layers':layers,'source_png_pivot':[.5,.5],
 'unity_center_pivot_conversion':'Para parent com âncoras top-left: anchoredPosition=[x+w/2,-(y+h/2)], sizeDelta=[w,h] no tamanho de desenho registrado.',
 'reuse_components':['UI_TOPBAR_VIOLET','UI_PANEL_IVORY','UI_INPUT_NORMAL','UI_PRIMARY_VIOLET_NORMAL'],
 'native_text':['Jogos de Empresa','Gestão de restaurantes','Identificação','Nome do aluno','RA','Cadastrar'],
 'pre_session_rule':'Não exibir caixa, score ou mês antes da sessão. Texto, formulário e botão são componentes/texto nativos, não partes do bitmap do cenário.',
 'integration_performed':False,'proposal_only':True}
write('docs/art/layouts/identification-layout.json',layout)
art=Image.new('RGBA',(1920,1080))
for layer in layers:art.alpha_composite(imgs[layer['asset_id']].resize(tuple(layer['draw_size_px']),Image.Resampling.LANCZOS),tuple(layer['position_px']))
art_path=P/'ART-14-art-layers.png';art.convert('RGB').save(art_path);previews.append(rel(art_path))
# Reuse validated components only in proof. No extra asset, Unity UI hierarchy or text sprite is generated.
assets={a['id']:a for a in m['assets']}
def component(id,size):
 a=assets[id];im=image(m['asset_root']+'/'+a['path']);return sliced(im,size,a['nine_slice_lbrt_px'][0]) if a['display_fit']=='sliced' else im.resize(size,Image.Resampling.LANCZOS)
composed=art.copy();composed.alpha_composite(component('UI_TOPBAR_VIOLET',(1920,88)),(0,0));composed.alpha_composite(component('UI_PANEL_IVORY',(650,772)),(1180,184))
for pos in [(1225,436),(1225,596)]:composed.alpha_composite(component('UI_INPUT_NORMAL',(560,80)),pos)
composed.alpha_composite(component('UI_PRIMARY_VIOLET_NORMAL',(560,96)),(1225,786))
d=ImageDraw.Draw(composed);titlefont=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',46);labelfont=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',25);headerfont=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',34)
d.text((40,23),'Jogos de Empresa',font=headerfont,fill='#FFFFFF');d.text((400,32),'Gestão de restaurantes',font=font,fill='#F5F0E8')
d.text((1225,254),'Identificação',font=titlefont,fill='#24113D');d.text((1225,394),'Nome do aluno',font=labelfont,fill='#24113D');d.text((1225,554),'RA',font=labelfont,fill='#24113D')
buttonfont=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',30);bounds=d.textbbox((0,0),'CADASTRAR',font=buttonfont);d.text((1505-(bounds[2]-bounds[0])/2,834-(bounds[3]-bounds[1])/2-bounds[1]),'CADASTRAR',font=buttonfont,fill='#FFFFFF')
comp_path=P/'ART-14-identification-composed.png';composed.convert('RGB').save(comp_path);previews.append(rel(comp_path));composed.resize((1280,720),Image.Resampling.LANCZOS).convert('RGB').save(P/'ART-14-identification-display.png');previews.append(rel(P/'ART-14-identification-display.png'))
# Crop examples: restaurant detail and quiet wall, with no foreground/form baked into the background.
for name,box in [('environment-detail',(180,40,1000,700)),('environment-form-space',(1180,184,1830,956))]:
 detail=P/(name+'.png');imgs['ID_ENVIRONMENT'].crop(box).convert('RGB').save(detail);previews.append(rel(detail))
summary={'png_count':4,'svg_count':2,'png_bytes':sum(r['output_bytes'] for r in records),'rgba8_capacity_bytes':sum(r['actual_size_px'][0]*r['actual_size_px'][1]*4 for r in records),
 'environment_bytes':lookup['ID_ENVIRONMENT']['output_bytes'],'cutouts_bytes':sum(r['output_bytes'] for r in records if r['id']!='ID_ENVIRONMENT'),
 'environment_initial_draft_export_bytes':2784175,'environment_reduction_percent':100*(1-lookup['ID_ENVIRONMENT']['output_bytes']/2784175),
 'memory_note':'Capacidade RGBA8 sem compressão/mipmaps; tamanho de build e memória real dependem de import/plataforma/atlas.'}
report={'task_id':'ART-14','date':'2026-10-14','passed':all(r['passed'] for r in results) and mirror_equal,'assets':results,
 'ornament_vector_mirror_equal':vector_mirror,'ornament_alpha_mirror_exact':bool(delta.max()==0),
 'ornament_mirror_antialias_difference':{'max_alpha_delta':int(delta.max()),'mean_alpha_delta':float(delta.mean()),'passed':mirror_equal},
 'summary':summary,'previews':previews,'layout_record':'docs/art/layouts/identification-layout.json',
 'preview_note':'Textos e componentes ART-03/04/05 aparecem somente na prova de composição. Os quatro PNGs ART-14 são separados e não têm texto/UI.',
 'visual_qa_required':True,'integration_performed':False}
write('docs/art/qa/ART-14-measurements.json',report)
print(json.dumps({'passed':report['passed'],'assets':[{k:r[k] for k in ['id','passed','actual_size_px','alpha_extrema']} for r in results],'ornament_mirror':mirror_equal,'summary':summary}));assert report['passed']
