"""Bounded read-only native font/icon provenance audit for the installed Unity22 files.

Never loads a game assembly or initializes a Unity type. Optional native candidate
PNG export is local analysis only; do not commit/package extracted textures/fonts.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
from PIL import Image
import visual_compare as vc

ROOT = Path(__file__).resolve().parents[1]
ICON_KEYS = {
    'plus': 'textures/ui/buttons/plus', 'search': 'textures/ui/widgets/search',
    'book': 'textures/ui/heroart/webicons/book', 'open-codex': 'textures/ui/buttons/opencodex',
    'learning-helper': 'textures/ui/buttons/showlearninghelper', 'search-toggle': 'textures/ui/widgets/searchtoggle',
}
FONT_KEYS = {'Tiny': 'fonts/calibri_tiny', 'Small': 'fonts/arial_small', 'Medium': 'fonts/arial_medium'}


class UnsupportedAsset(ValueError): pass


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''): digest.update(chunk)
    return digest.hexdigest()


class Cursor:
    def __init__(self, data, offset=0): self.data, self.at = data, offset
    def read(self, fmt):
        size=struct.calcsize(fmt)
        if self.at < 0 or self.at+size > len(self.data): raise UnsupportedAsset('Truncated serialized field')
        values=struct.unpack_from(fmt,self.data,self.at);self.at+=size
        return values[0] if len(values)==1 else values
    def skip(self, size):
        if size<0 or self.at+size>len(self.data): raise UnsupportedAsset('Invalid serialized span')
        self.at+=size
    def count(self, maximum=200000):
        value=self.read('<i')
        if not 0<=value<=maximum: raise UnsupportedAsset('Invalid bounded array count')
        return value
    def align(self): self.at=(self.at+3)&~3
    def zero_string(self):
        end=self.data.find(b'\0',self.at)
        if end<0 or end-self.at>4096: raise UnsupportedAsset('Invalid terminated metadata string')
        value=self.data[self.at:end].decode('utf-8');self.at=end+1;return value
    def string(self):
        size=self.count(4096)
        if self.at+size>len(self.data):raise UnsupportedAsset('Invalid string span')
        value=self.data[self.at:self.at+size].decode('utf-8');self.at+=size;self.align();return value


def serialized_file(data):
    if len(data)<48 or struct.unpack_from('>I',data,8)[0]!=22: raise UnsupportedAsset('Only serialized-file version22 is supported')
    if data[16]!=0: raise UnsupportedAsset('Only little-endian asset metadata is supported')
    metadata_size,file_size,data_offset=struct.unpack_from('>IQQ',data,20)
    if file_size!=len(data) or not 48<=metadata_size<=data_offset<=len(data): raise UnsupportedAsset('Header size/offset mismatch')
    cur=Cursor(data,48);unity=cur.zero_string();platform=cur.read('<i');tree=cur.read('<?')
    if tree:raise UnsupportedAsset('Type-tree files are not parsed by this bounded stripped-type reader')
    types=[]
    for _ in range(cur.count(4096)):
        class_id=cur.read('<i');cur.read('<?');cur.read('<h')
        if class_id==114:cur.skip(16)
        cur.skip(16);types.append(class_id)
    objects={}
    for _ in range(cur.count()):
        cur.align();path_id=cur.read('<q');offset=cur.read('<q')+data_offset;size=cur.read('<I');index=cur.read('<i')
        if path_id in objects or not 0<=index<len(types) or offset<data_offset or offset+size>len(data): raise UnsupportedAsset('Duplicate object ID or invalid object type/span')
        objects[path_id]={'path_id':path_id,'offset':offset,'size':size,'class_id':types[index]}
    for _ in range(cur.count()):cur.read('<i');cur.align();cur.read('<q')
    external=[]
    for _ in range(cur.count(4096)):
        cur.zero_string();cur.skip(16);cur.read('<i');external.append(cur.zero_string())
    return {'unity_version':unity,'platform':platform,'data_offset':data_offset,'objects':objects,'external_files':external}


def object_bytes(blob,record):return blob[record['offset']:record['offset']+record['size']]


def resources(blob,metadata):
    result=[]
    for obj in metadata['objects'].values():
        if obj['class_id']!=147:continue
        cur=Cursor(object_bytes(blob,obj))
        for _ in range(cur.count()):
            key=cur.string();file_id,path_id=cur.read('<iq')
            if not 0<=file_id<=len(metadata['external_files']):raise UnsupportedAsset('ResourceManager has unresolved external file ID')
            result.append({'resource':key,'file_id':file_id,'file':metadata['external_files'][file_id-1] if file_id else '<self>','path_id':path_id})
    return result


def font_spans(raw):
    results=[]
    for signature in (b'\x00\x01\x00\x00',b'OTTO'):
        at=0
        while True:
            off=raw.find(signature,at)
            if off<0:break
            at=off+1
            if off<4 or off+12>len(raw):continue
            count=struct.unpack_from('>H',raw,off+4)[0]
            if not 1<=count<=128 or off+12+count*16>len(raw):continue
            tables={}
            for i in range(count):
                tag,_,start,size=struct.unpack_from('>4sIII',raw,off+12+i*16);tables[tag]=(start,size)
            if not {b'head',b'name',b'cmap'}<=tables.keys():continue
            length=struct.unpack_from('<I',raw,off-4)[0]
            if not max(start+size for start,size in tables.values())<=length<=len(raw)-off:continue
            start,size=tables[b'name'];table=raw[off+start:off+start+size]
            if len(table)<6:continue
            _,name_count,string_offset=struct.unpack_from('>HHH',table)
            if 6+name_count*12>len(table):continue
            names={};valid=True
            for i in range(name_count):
                platform,encoding,language,name_id,nlen,noff=struct.unpack_from('>HHHHHH',table,6+i*12)
                if string_offset+noff+nlen>len(table):valid=False;break
                if name_id not in (1,2,4,5,6):continue
                text=table[string_offset+noff:string_offset+noff+nlen].decode('utf-16-be' if platform in (0,3) else 'mac_roman',errors='replace')
                names.setdefault(str(name_id),set()).add(text)
            if valid:results.append({'offset_in_object':off,'byte_length':length,'sha256':hashlib.sha256(raw[off:off+length]).hexdigest(),'name_table':{k:sorted(v) for k,v in names.items()}})
    return results


def texture_info(raw):
    cur=Cursor(raw);name=cur.string();base=cur.at
    # Verified layout for this Unity2022.3 stripped Texture2D serialization only.
    if len(raw)<base+32:raise UnsupportedAsset('Truncated Texture2D metadata')
    width,height=struct.unpack_from('<ii',raw,base+8);fmt,mips=struct.unpack_from('<ii',raw,base+24)
    if not 1<=width<=1024 or not 1<=height<=1024 or not 1<=mips<=16:raise UnsupportedAsset('Unsupported candidate dimensions/mip count')
    marker=b'resources.assets.resS';positions=[m.start() for m in re.finditer(re.escape(marker),raw)]
    if len(positions)!=1:raise UnsupportedAsset('Candidate stream filename not explicit/unique')
    at=positions[0]
    if at<16 or struct.unpack_from('<I',raw,at-4)[0]!=len(marker):raise UnsupportedAsset('Invalid stream-path length/offset')
    offset,size=struct.unpack_from('<QI',raw,at-16)
    return {'name':name,'width':width,'height':height,'texture_format':fmt,'mip_count':mips,'stream_file':marker.decode(),'stream_offset':offset,'stream_size':size}


def texture_payload(data_dir,record):
    path=(data_dir/record['stream_file']).resolve()
    if path.parent!=data_dir.resolve() or not path.is_file():raise UnsupportedAsset('Texture stream escapes data directory or is missing')
    if record['stream_offset']+record['stream_size']>path.stat().st_size:raise UnsupportedAsset('Texture stream span exceeds source file')
    with path.open('rb') as stream:stream.seek(record['stream_offset']);payload=stream.read(record['stream_size'])
    return path,payload


def decode_texture(record,payload):
    width,height,fmt=record['width'],record['height'],record['texture_format']
    length=width*height*4 if fmt==4 else ((width+3)//4)*((height+3)//4)*16 if fmt==12 else None
    if length is None or len(payload)<length:raise UnsupportedAsset('Unsupported or truncated RGBA32/DXT5 payload')
    image=Image.frombytes('RGBA',(width,height),payload[:length]) if fmt==4 else Image.frombytes('RGBA',(width,height),payload[:length],'bcn',(3,'DXT5'))
    return image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)


def verify_source_hashes(expected,observed):
    if expected!=observed:raise UnsupportedAsset('Source file SHA-256 identity mismatch')


def audit(game_root,output,export_candidates=False,expected_report=None):
    game=Path(game_root).resolve();data_dir=game/'RimWorldWin64_Data';assembly=data_dir/'Managed/Assembly-CSharp.dll'
    required=[assembly,data_dir/'globalgamemanagers',data_dir/'resources.assets',data_dir/'resources.assets.resS']
    before={str(path):sha256(path) for path in required}
    if expected_report is not None:
        expected=json.loads(Path(expected_report).read_text(encoding='utf-8-sig'))
        verify_source_hashes(expected['source_files'],before)
    out=vc.prepare_output(output,required)
    helper=ROOT/'Tests/CaptureContracts/NativeMetadata/NativeMetadata.csproj'
    command=['dotnet','run','--project',str(helper),'--configuration','Release','--no-launch-profile','--',str(assembly),str(out/'assembly-metadata.json')]
    called=subprocess.run(command,cwd=ROOT,capture_output=True,text=True)
    (out/'metadata-reader.log').write_text(called.stdout+called.stderr,encoding='utf-8')
    if called.returncode:raise UnsupportedAsset('Metadata helper failed; inspect metadata-reader.log')
    il=json.loads((out/'assembly-metadata.json').read_text())
    packed=(data_dir/'resources.assets').read_bytes();meta=serialized_file(packed)
    manager_blob=(data_dir/'globalgamemanagers').read_bytes();manager=serialized_file(manager_blob);keys=resources(manager_blob,manager)
    textures=[]
    for icon,key in ICON_KEYS.items():
        entries=[e for e in keys if e['resource']==key]
        if len(entries)!=1 or entries[0]['file']!='resources.assets':raise UnsupportedAsset(f'{key}: missing/ambiguous native file binding')
        obj=meta['objects'].get(entries[0]['path_id'])
        if obj is None or obj['class_id']!=28:raise UnsupportedAsset(f'{key}: native target is not Texture2D')
        raw=object_bytes(packed,obj);info=texture_info(raw)
        # Resource basename must identify the serialized native object (case-insensitive).
        if info['name'].lower()!=key.rsplit('/',1)[1]:raise UnsupportedAsset('Resource key/object name mismatch')
        stream,payload=texture_payload(data_dir,info)
        record={'id':icon,'resource':key,'resources_load_path':key.removeprefix('textures/'),'object':obj,'serialized_object_sha256':hashlib.sha256(raw).hexdigest(),
                **info,'stream_sha256':hashlib.sha256(payload).hexdigest(),'visual_source_match':'unproved native candidate'}
        if export_candidates:
            target=out/(icon+'.png');decode_texture(info,payload).save(target)
            record['local_png']={'path':str(target),'sha256':sha256(target),'transform':'Unity lower-left storage -> PNG upper-left vertical flip; no tint/resize; decoded base mip only'}
        textures.append(record)
    fonts=[]
    for obj in meta['objects'].values():
        if obj['class_id']!=128:continue
        raw=object_bytes(packed,obj);name=Cursor(raw).string()
        fonts.append({'name':name,'object':obj,'serialized_object_sha256':hashlib.sha256(raw).hexdigest(),'embedded_sfnt':font_spans(raw),
                      'resource_keys':[e['resource'] for e in keys if e['file']=='resources.assets' and e['path_id']==obj['path_id']],
                      'limit':'Embedded bytes establish this native object payload only, not the live per-glyph fallback face or concept typography'})
    for slot,key in FONT_KEYS.items():
        hits=[f for f in fonts if key in f['resource_keys']]
        if len(hits)!=1:raise UnsupportedAsset(f'{slot}: missing/ambiguous Font resource binding')
    after={str(path):sha256(path) for path in required}
    verify_source_hashes(before,after)
    result={'schema':'native-asset-provenance-v1','status':'pass','meaning':'bounded native identity/byte-span audit only; no source visual approval',
            'source_acceptance':False,'target_assembly_executed':False,'source_files':before,'unity_version':meta['unity_version'],
            'metadata_reader':{'method_bodies':il['method_bodies_read'],'parse_failures':il['parse_failures'],'resource_literals_seen':il['resource_literals_seen'],'command':command},
            'packed_objects':len(meta['objects']),'resource_keys':len(keys),'textures':textures,'fonts':fonts,'text_style_resource_contract':FONT_KEYS,
            'coverage':{'found_native_candidates':['plus','search','book','open-codex','learning-helper','search-toggle'],
                        'no_matching_UI_key_or_literal_in_scanned_install':['load-tray','tutorial-scroll','cog','briefcase','door-exit'],
                        'not_proved':['source pixel equality','active per-glyph face','live source-sized Unity texture draw/tint/filter','all possible mod/DLC/dynamic asset sources']},
            'exports':'six bounded local candidate PNGs only' if export_candidates else 'metadata and hashes only; no font/texture bytes exported'}
    if il['parse_failures']:result['status']='incomplete'
    (out/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    return result


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root',required=True);parser.add_argument('--output',required=True);parser.add_argument('--export-candidates',action='store_true');parser.add_argument('--expected-report')
    args=parser.parse_args(argv)
    try:
        result=audit(args.game_root,args.output,args.export_candidates,args.expected_report)
        print(json.dumps({'status':result['status'],'textures':len(result['textures']),'fonts':len(result['fonts']),'source_acceptance':False}))
        return 0 if result['status']=='pass' else 1
    except (UnsupportedAsset,vc.InvalidInput,OSError,ValueError,KeyError,struct.error) as exc:
        print(json.dumps({'status':'invalid_or_unsupported','error':str(exc)}),file=sys.stderr);return 2


if __name__=='__main__':raise SystemExit(main())