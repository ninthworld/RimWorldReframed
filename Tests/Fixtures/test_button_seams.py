"""Known positive/negative sideband probes; no synthetic image is game proof."""
from pathlib import Path
import sys
import unittest
from PIL import Image,ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'Tools'))
import check_button_seams as seams
import validate_foundation_run as vr


class SeamTests(unittest.TestCase):
    def fixture(self,scale=1):
        image=Image.new('RGB',(1200,450),(14,19,23))
        draw=ImageDraw.Draw(image)
        origin=(10,7)
        # Solid known-color cards with intentionally unrelated center text/fringes.
        colors=[(33,40,50),(45,56,71),(24,31,40),(39,47,60)]
        for index,color in enumerate(colors):
            x,y=origin[0]+(16+145*index)*scale,origin[1]+82*scale
            draw.rectangle([round(x),round(y),round(x+133*scale)-1,round(y+36*scale)-1],fill=color)
            draw.rectangle([round(x+50*scale),round(y+12*scale),round(x+80*scale),round(y+22*scale)],fill='white')
            draw.line([(round(x+3*scale),round(y)),(round(x+130*scale),round(y))],fill='red')
        return image,origin

    def test_clean_bands_ignore_text_and_top_fringe(self):
        image,origin=self.fixture()
        result,_,_=seams.analyze(image,origin,1)
        self.assertEqual(result['status'],'pass')
        self.assertEqual(result['included_pixels'],1080)
        self.assertEqual(result['bad_pixels'],0)

    def test_one_black_pixel_has_known_location_and_error(self):
        image,origin=self.fixture()
        image.putpixel((29,99),(0,0,0)) # Normal left band; independently chosen.
        result,_,_=seams.analyze(image,origin,1)
        self.assertEqual(result['status'],'fail')
        self.assertEqual(result['bad_pixels'],1)
        band=result['bands'][0]
        self.assertEqual(band['difference_bbox_client_xywh'],[29,99,1,1])
        self.assertEqual(band['maximum_channel_error'],50)
        self.assertEqual(band['total_absolute_channel_error'],123)

    def test_vertical_seam_detects_twenty_seven_pixels(self):
        image,origin=self.fixture()
        for y in range(94,121):image.putpixel((29,y),(0,0,0))
        result,_,_=seams.analyze(image,origin,1)
        self.assertEqual(result['bad_pixels'],27)
        self.assertEqual(result['bands'][0]['difference_bbox_client_xywh'],[29,94,1,27])

    def test_altered_text_corner_and_excluded_bottom_are_not_seams(self):
        image,origin=self.fixture()
        for point in [(26,89),(27,124),(95,107)]:image.putpixel(point,(255,0,0))
        result,_,_=seams.analyze(image,origin,1)
        self.assertEqual(result['bad_pixels'],0)

    def test_fractional_scale_has_fixed_pixel_center_coverage(self):
        image,origin=self.fixture(1.5)
        result,_,_=seams.analyze(image,origin,1.5)
        self.assertEqual(result['status'],'pass')
        self.assertEqual(result['included_pixels'],2460)
        # Left/right starts differ126 UI units =189 pixels: same half-pixel phase.
        self.assertEqual([b['included_pixels'] for b in result['bands']], [328,328,287,287,328,328,287,287])

    def test_alpha_difference_is_not_silently_ignored(self):
        image,origin=self.fixture()
        image=image.convert('RGBA');image.putpixel((29,99),(33,40,50,254))
        result,_,_=seams.analyze(image,origin,1)
        self.assertEqual(result['bad_pixels'],1)
        self.assertEqual(result['bands'][0]['maximum_channel_error'],1)

    def test_unsupported_scale_and_out_of_bounds_do_not_pass(self):
        image,origin=self.fixture()
        for bad_origin,bad_scale in [(origin,1.25),((-1,0),1),((900,0),1),((1.5,0),1)]:
            with self.assertRaises(vr.EvidenceError):seams.analyze(image,bad_origin,bad_scale)

    def test_interval_uses_half_open_pixel_centers(self):
        self.assertEqual(seams.pixel_interval(1.5,9),(1,9))
        self.assertEqual(seams.pixel_interval(1,6),(1,6))
        with self.assertRaises(vr.EvidenceError):seams.pixel_interval(2,2)


if __name__=='__main__':unittest.main(verbosity=2)