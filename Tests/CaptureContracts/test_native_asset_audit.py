"""Bounded native provenance parser negatives plus actual specimen5 seam contract."""
from pathlib import Path
import hashlib
import json
import struct
import sys
import tempfile
import unittest
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools'))
import native_asset_audit as native
import check_button_seams as seams
import validate_foundation_run as vr


def packed_string(value):
    data=value.encode();result=struct.pack('<i',len(data))+data
    return result+b'\0'*((-len(result))%4)


def unity_fixture(objects):
    # Minimal version22 stripped-type file, one Texture2D class, no externals.
    buf=bytearray(48)
    buf+=b'2022.3.35f1\0'+struct.pack('<i?i',19,False,1)+struct.pack('<i?h',28,False,-1)+b'\0'*16
    buf+=struct.pack('<i',len(objects));positions=[];offset=0
    for pid,data in objects:
        buf+=b'\0'*((-len(buf))%4);positions.append(len(buf))
        buf+=struct.pack('<qqIi',pid,offset,len(data),0);offset+=len(data)
    buf+=struct.pack('<ii',0,0)
    metadata_size=len(buf)-48;data_offset=(len(buf)+15)&~15;buf+=b'\0'*(data_offset-len(buf))
    for pid,data in objects:buf+=data
    struct.pack_into('>IIII',buf,0,0,0,22,0);buf[16]=0
    struct.pack_into('>IQQQ',buf,20,metadata_size,len(buf),data_offset,0)
    return bytes(buf),positions


class NativeAssetTests(unittest.TestCase):
    def setUp(self):
        scratch=(ROOT/'work/native-asset-tests').resolve();scratch.mkdir(parents=True,exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(dir=scratch,prefix='case-');self.root=Path(self.temp.name).resolve()
        self.root.relative_to(scratch);self.addCleanup(self.temp.cleanup)

    def test_known_object_name_and_bounds_resolve(self):
        raw=packed_string('Plus');blob,_=unity_fixture([(719,raw)])
        meta=native.serialized_file(blob);obj=meta['objects'][719]
        self.assertEqual(obj['class_id'],28)
        self.assertEqual(native.Cursor(native.object_bytes(blob,obj)).string(),'Plus')

    def test_file_header_hash_and_object_offset_faults_detected(self):
        blob,positions=unity_fixture([(719,packed_string('Plus'))])
        bad=bytearray(blob);struct.pack_into('>Q',bad,24,len(blob)+1)
        with self.assertRaisesRegex(native.UnsupportedAsset,'Header'):native.serialized_file(bad)
        bad=bytearray(blob);struct.pack_into('<q',bad,positions[0]+8,10**8)
        with self.assertRaisesRegex(native.UnsupportedAsset,'span'):native.serialized_file(bad)
        with self.assertRaisesRegex(native.UnsupportedAsset,'SHA-256'):
            native.verify_source_hashes({'resources.assets':'a'*64},{'resources.assets':'b'*64})

    def test_duplicate_ids_and_unsupported_versions_fail_closed(self):
        blob,_=unity_fixture([(719,b'a'),(719,b'b')])
        with self.assertRaisesRegex(native.UnsupportedAsset,'Duplicate'):native.serialized_file(blob)
        blob,_=unity_fixture([(719,b'a')]);bad=bytearray(blob);struct.pack_into('>I',bad,8,21)
        with self.assertRaisesRegex(native.UnsupportedAsset,'version22'):native.serialized_file(bad)

    def test_invalid_name_span_is_not_an_empty_name(self):
        with self.assertRaises(native.UnsupportedAsset):native.Cursor(struct.pack('<i',100)+b'ab').string()

    def test_resource_manager_bad_file_reference_is_not_candidate_absence(self):
        raw=struct.pack('<i',1)+packed_string('textures/ui/buttons/plus')+struct.pack('<iq',8,719)
        meta={'objects':{1:{'offset':0,'size':len(raw),'class_id':147}},'external_files':['resources.assets']}
        with self.assertRaisesRegex(native.UnsupportedAsset,'external file ID'):native.resources(raw,meta)

    def test_texture_stream_bounds_and_path_cannot_escape(self):
        stream=self.root/'resources.assets.resS';stream.write_bytes(b'1234')
        with self.assertRaisesRegex(native.UnsupportedAsset,'span'):
            native.texture_payload(self.root,{'stream_file':stream.name,'stream_offset':3,'stream_size':2})
        with self.assertRaisesRegex(native.UnsupportedAsset,'escapes'):
            native.texture_payload(self.root,{'stream_file':'../outside','stream_offset':0,'stream_size':1})

    def test_known_raw_texture_orientation_and_truncation(self):
        # Bottom row red/green, top row blue/white in Unity storage.
        record={'width':2,'height':2,'texture_format':4}
        payload=bytes([255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255])
        image=native.decode_texture(record,payload)
        self.assertEqual(image.getpixel((0,0)),(0,0,255,255))
        self.assertEqual(image.getpixel((1,1)),(0,255,0,255))
        with self.assertRaisesRegex(native.UnsupportedAsset,'truncated'):native.decode_texture(record,payload[:4])

    def test_bounded_embedded_font_hash_and_name(self):
        text='Test'.encode('utf-16-be')
        names=struct.pack('>HHH',0,1,18)+struct.pack('>HHHHHH',3,1,0x409,1,len(text),0)+text
        font=struct.pack('>IHHHH',0x10000,3,0,0,0)
        font+=struct.pack('>4sIII',b'head',0,60,4)+struct.pack('>4sIII',b'cmap',0,64,4)+struct.pack('>4sIII',b'name',0,68,len(names))
        font+=b'\0'*8+names
        raw=b'prefix'+struct.pack('<I',len(font))+font
        spans=native.font_spans(raw)
        self.assertEqual(len(spans),1)
        self.assertEqual(spans[0]['sha256'],hashlib.sha256(font).hexdigest())
        self.assertEqual(spans[0]['name_table']['1'],['Test'])
        self.assertEqual(native.font_spans(raw[:-1]),[])

    def test_specimen5_unchanged_geometry_is_accepted_not_future_versions(self):
        image=Image.new('RGB',(760,286),(14,19,23));draw=ImageDraw.Draw(image)
        for i,color in enumerate([(33,40,50),(45,56,71),(24,31,40),(39,47,60)]):
            x=16+145*i;draw.rectangle((x,82,x+132,117),fill=color)
        colors={'top_left':([0,0],[255,0,255,255]),'top_right':([759,0],[0,255,255,255]),
                'bottom_left':([0,285],[255,255,0,255]),'bottom_right':([759,285],[255,255,255,255])}
        for point,color in colors.values():image.putpixel(tuple(point),tuple(color[:3]))
        png=self.root/'capture.png';image.save(png)
        request=self.root/'request.json';request_data={'specimen_page':'primitives','specimen_version':5,'png_sha256':native.sha256(png),
          'client_width':760,'client_height':286,'ui_scale':1,'specimen_gui_to_screen':[0,0,760,286]}
        request.write_text(json.dumps(request_data))
        evidence=self.root/'notes.txt';evidence.write_text('Synthetic unchanged geometry contract, not game proof')
        mapping=self.root/'mapping.json';mapping.write_text(json.dumps({'schema':'reframed-foundation-mapping-v1','status':'observed',
          'request_sha256':native.sha256(request),'png_sha256':native.sha256(png),'client_dimensions':[760,286],'ui_scale':1,
          'reported_rect_xywh':[0,0,760,286],'client_rect_xywh':[0,0,760,286],'method':'synthetic corners','reason':'known synthetic fixture',
          'evidence':{'path':str(evidence),'sha256':native.sha256(evidence)},
          'fiducials':[{'name':name,'client_xy':point,'rgba':color} for name,(point,color) in colors.items()]}))
        self.assertEqual(seams.check(png,request,mapping,self.root/'out')['bad_pixels'],0)
        request_data['specimen_version']=6;request.write_text(json.dumps(request_data))
        with self.assertRaisesRegex(vr.EvidenceError,'reviewed contract'):seams.check(png,request,mapping,self.root/'unsupported')


if __name__=='__main__':unittest.main(verbosity=2)