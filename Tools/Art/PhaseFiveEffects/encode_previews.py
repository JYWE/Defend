"""Encode real Unity capture frames without fabricating or retouching their contents."""
from pathlib import Path
from PIL import Image
import json, shutil

root=Path(__file__).resolve().parents[3]
output=root/'Docs/Art/PhaseFiveEffects/Previews'
output.mkdir(parents=True,exist_ok=True)
records=[]
for name in ('Overview','Close','Damage','Energy','Shield'):
    paths=sorted((root/'Logs/PhaseFiveEffects/Frames'/name).glob('frame_*.png'))
    if len(paths)!=96:
        raise RuntimeError(f'{name}: expected 96 frames, found {len(paths)}')
    frames=[]
    for path in paths:
        with Image.open(path) as source:
            frame=source.convert('RGB')
            frame.thumbnail((960,800),Image.Resampling.LANCZOS)
            frames.append(frame)
    sample=Image.new('RGB',(frames[0].width*8,frames[0].height*12))
    for index,frame in enumerate(frames):sample.paste(frame,((index%8)*frame.width,(index//8)*frame.height))
    palette=sample.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    quantized=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
    durations=[30 if i%3!=2 else 40 for i in range(96)]
    target=output/(name+'.gif')
    quantized[0].save(target,save_all=True,append_images=quantized[1:],duration=durations,loop=0,disposal=1,optimize=False)
    with Image.open(target) as reopened:
        count=reopened.n_frames
        duration=sum((reopened.seek(i) or reopened.info['duration']) for i in range(count))
    selected={'Overview':10,'Close':10,'Damage':48,'Energy':9,'Shield':67}[name]
    shutil.copyfile(paths[selected],output/(name+'.png'))
    records.append({'name':name,'source_frames':len(paths),'gif_frames':count,'milliseconds':duration,'bytes':target.stat().st_size,'source':'Unity URP RenderRequest in actual Play Mode, fixed 1/30 second presentation steps','performance_evidence':False})
(root/'Docs/Art/PhaseFiveEffects/Checks/PreviewEncoding.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps(records,indent=2))
