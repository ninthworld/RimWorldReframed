from pathlib import Path
import sys,re,json,csv,hashlib,zipfile,html,shutil,argparse
from urllib.parse import unquote
# Local bootstrap dependencies are optional; installed-project use takes markdown2
# from its selected Python environment (see Tools/requirements-docs.txt).
bootstrap=Path('work/design-audit/python-deps')
if bootstrap.is_dir():sys.path.insert(0,str(bootstrap.resolve()))
import markdown2
parser=argparse.ArgumentParser(description='Rebuild the illustrated specification and verify package integrity.')
parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]/'Docs/UI-Reworked')
parser.add_argument('--source-only',action='store_true',help='Build an explicitly evidence-free source snapshot; never claim absent reference hashes were verified.')
args=parser.parse_args()
R=args.root.resolve()
docs=['01-design-specification.md','02-test-regimen.md','03-work-order-plan.md','04-team-workflow.md','05-inferred-design-policy.md','06-repository-setup.md','07-github-readiness.md','Audit32/README.md']
parts=[];nav=[]
for i,name in enumerate(docs,1):
 text=(R/name).read_text(encoding='utf-8')
 # Use file prefixes so identical section headings cannot collide across documents.
 rendered=str(markdown2.markdown(text,extras=['tables','fenced-code-blocks','header-ids']))
 ids=re.findall(r'<h([123]) id="([^"]+)">(.+?)</h\1>',rendered)
 for level,ident,title in ids:
  new=f'doc{i}-{ident}'
  rendered=rendered.replace('id="'+ident+'"','id="'+new+'"')
  if level in ('1','2'):
   nav.append(f'<a class="level{level}" href="#{new}">{re.sub("<[^>]+>","",title)}</a>')
 parts.append('<article>'+rendered+'</article>')
# Articles nested in Audit32 keep their own relative Markdown links; normalize
# these to the report's root before combining them into one HTML document.
for i,name in enumerate(docs):
 parent=Path(name).parent
 if parent != Path('.'):
  def relocate(match):
   attr,url=match.group(1),match.group(2)
   if url.startswith(('http:','https:','#','data:')):return match.group(0)
   import posixpath
   return attr+'="'+posixpath.normpath((parent/Path(url)).as_posix())+'"'
  parts[i]=re.sub(r'(href|src)="([^"]+)"',relocate,parts[i])
content='\n'.join(parts)
for i,name in enumerate(docs,1):
 first=re.search(r'<h1 id="([^"]+)"',parts[i-1]).group(1)
 content=content.replace('href="'+name+'"','href="#'+first+'"')
css='''
:root{color-scheme:light;--ink:#172735;--sub:#526579;--line:#ccd7e0;--accent:#125e7d}
*{box-sizing:border-box}body{margin:0;background:#f4f7fa;color:var(--ink);font:16px/1.65 "Segoe UI",sans-serif}
aside{position:fixed;inset:0 auto 0 0;width:270px;background:#101b24;color:#d8e2ea;padding:26px 22px;overflow:auto}
aside strong{font-size:18px;display:block;margin-bottom:8px}aside p{color:#9eafbe;font-size:13px;line-height:1.5}
nav a{display:block;color:#bdcfdd;text-decoration:none;font-size:13px;line-height:1.4;padding:7px 0}nav a:hover{color:white}
nav .level1{font-weight:700;color:white;border-top:1px solid #30414f;margin-top:14px;padding-top:14px}
main{margin-left:270px;padding:42px 5vw 80px;max-width:1550px}.cover{background:#173649;color:white;border-radius:14px;padding:32px;margin-bottom:30px}.cover p{color:#c9d9e5}.cover a{color:#a9e3f5}
article{background:white;border:1px solid var(--line);border-radius:14px;padding:32px 40px;margin-bottom:30px;overflow:hidden}
h1{font-size:31px;line-height:1.25;margin:0 0 20px}h2{font-size:24px;line-height:1.35;margin:44px 0 18px;border-top:1px solid var(--line);padding-top:24px}h3{font-size:20px;line-height:1.35;margin-top:34px}
p{margin:13px 0}a{color:var(--accent);text-underline-offset:3px}img{max-width:100%;height:auto;display:block;border-radius:5px;margin:18px 0;background:#0e1317}table{border-collapse:collapse;width:100%;font-size:14px;margin:20px 0;display:block;overflow:auto}th,td{text-align:left;vertical-align:top;padding:10px 12px;border:1px solid var(--line)}th{background:#eaf0f5}tr:nth-child(even){background:#f8fafc}code{background:#eaf0f5;border-radius:3px;padding:2px 4px;font-size:13px}pre{background:#eaf0f5;padding:18px;overflow:auto;line-height:1.6}pre code{padding:0}li{margin:7px 0}:target{scroll-margin-top:22px}footer{color:var(--sub);font-size:13px}
@media(max-width:900px){aside{position:relative;width:auto}nav{columns:2}main{margin:0;padding:20px}article{padding:24px}h1{font-size:27px}}
@media print{aside{display:none}main{margin:0;padding:0}article{border:0;padding:10px;break-before:page}table{font-size:10px}body{font-size:11px}h1{font-size:24px}h2{font-size:18px}h3{font-size:15px}img{max-height:700px;object-fit:contain}tr{break-inside:avoid}.cover{background:white;color:black;border:1px solid #ccd7e0}.cover p,.cover a{color:black}}
'''
page='<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>RimWorld UI Reworked — reconstruction specification</title><style>'+css+'</style></head><body><aside><strong>RimWorld UI Reworked</strong><p>Reconstruction specification<br>Revision 34 · 18 October 2026</p><nav>'+''.join(nav)+'</nav></aside><main><section class="cover"><h1>Reference, implementation scope, and in-game proof</h1><p>21 component projects · team and handoff rules · inferred UI coverage</p><p>Functional delivery and visual completion are tracked independently. v0.23.1 is installed with compact colony menus, the restored research graph, modern History chart and clearer controls. Panels rise from the bottom; dropdowns unfold beside their triggers. Startup is verified and the user tests. Batch30 separates private checks and deployment from pending live acceptance; earlier batches retain historical evidence. Both animation directions and slight transparency remain required for every window; older coverage remains open. No project has visual completion or final acceptance recorded.</p><p><a href="README.md">Editable documents and evidence index</a> · <a href="project-register.csv">Project progress register</a> · <a href="visual-completion.md">Visual completion backlog</a> · <a href="HANDOFF.md">Resume here</a> · <a href="Features/Batch30/README.md">Latest feature build and tests</a> · <a href="inference-register.csv">Inference register</a> · <a href="measurements/README.md">Pixel measurements</a> · <a href="source-manifest.json">Source manifest</a></p></section>__PROJECT_STATUS__'+content+'<footer>Concept artwork: yarich SA, RimWorld UI Reworked. Analysis and planning: 17 October 2026. Full source fidelity is not accepted. Named feature results and limits are recorded separately in the batch report.</footer></main></body></html>'
page=page.replace('Revision 34 · 18 October 2026','Revision 35 · 19 October 2026')
page=page.replace('21 component projects · team and handoff rules · inferred UI coverage','21 component projects · complete native hierarchy inventory · actionable UI backlog · GitHub preparation')
page=re.sub(r'<p>Functional delivery and visual completion are tracked independently\..*?</p>', '<p>Audit32 inventories 715 native UI declarations and helper candidates, with an actionable TODO and separate source/live evidence. v0.23.1 remains installed; no UI implementation is changed by this preparation. A fresh source-only repository is prepared for continued GitHub development while local reference art and history remain preserved. All previous visual and functional acceptance gates remain open.</p>', page, count=1)
page=page.replace('<a href="README.md">Editable documents and evidence index</a>', '<a href="Audit32/README.md">Current UI audit and TODO</a> · <a href="07-github-readiness.md">GitHub readiness</a> · <a href="README.md">Editable documents and evidence index</a>')
if args.source_only:
 for omitted in ['Features/Batch30/README.md','measurements/README.md','source-manifest.json']:
  page=page.replace('href="'+omitted+'"','href="LOCAL_EVIDENCE.md"')
# CSV register gives future work orders stable project and test IDs.
main=(R/docs[0]).read_text(encoding='utf-8')
projects=re.findall(r'^### (P\d+) (.+)$',main,re.M)
register_path=R/'project-register.csv'
fields=['project_id','title','implementation_order','test_family','status','functional_status','functional_evidence','visual_status','visual_backlog','visual_evidence','final_acceptance']
previous={}
if register_path.exists():
 with register_path.open(newline='',encoding='utf-8-sig') as f:
  reader=csv.DictReader(f)
  fields=list(dict.fromkeys(fields+list(reader.fieldnames or [])))
  previous={row['project_id']:row for row in reader}
with register_path.open('w',newline='',encoding='utf-8') as f:
 w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
 order={'P01':'W01','P02':'W02','P03':'W03','P04':'W04','P05':'W06','P06':'W07','P07':'W05','P08':'W08','P09':'W09','P10':'W10','P11':'W11','P12':'W12','P13':'W13','P14':'W14','P15':'W15','P16':'W16','P17':'W17','P18':'W18','P19':'W19','P20':'W20','P21':'W21'}
 for id,title in projects:
  row={'project_id':id,'title':title,'implementation_order':order[id],'test_family':id,'status':'planned; not implemented or tested in this phase','functional_status':'Not recorded; verify the current execution state.','functional_evidence':'','visual_status':'Incomplete; no project-level visual acceptance recorded.','visual_backlog':'See visual-completion.md.','visual_evidence':'No matched-scale comparison or approved visual baseline recorded.','final_acceptance':'Not accepted; visual completion and final work-order gates remain open.'}
  row.update(previous.pop(id,{}))
  row['title']=title
  w.writerow(row)
 for row in previous.values():w.writerow(row)
assert len(projects)==21
# Keep the generated report readable while preserving the full row-level register.
with register_path.open(newline='',encoding='utf-8-sig') as f:
 register_rows=list(csv.DictReader(f))
project_anchors={pid:anchor for anchor,pid in re.findall(r'<h[1-6] id="(doc1-[^"]+)">(P\d{2})\b',parts[0])}
status_rows=[]
for row in register_rows:
 pid=html.escape(row.get('project_id',''))
 anchor=project_anchors.get(row.get('project_id',''))
 project=f'<a href="#{html.escape(anchor)}">{pid}</a>' if anchor else pid
 status_rows.append('<tr><td>'+project+' '+html.escape(row.get('title',''))+'</td><td>'+html.escape(row.get('functional_status',''))+'</td><td>'+html.escape(row.get('visual_status',''))+' <a href="visual-completion.md">Backlog</a></td><td>'+html.escape(row.get('final_acceptance',''))+'</td></tr>')
status_section='<article><h2>Project progress and visual completion</h2><p>Functional evidence and visual acceptance are separate. A usable increment or passing feature check does not establish a source match. See the <a href="project-register.csv">complete project register</a> and <a href="visual-completion.md">source-specific visual backlog</a>.</p><table><thead><tr><th>Project</th><th>Functional progress</th><th>Visual completion</th><th>Final acceptance</th></tr></thead><tbody>'+''.join(status_rows)+'</tbody></table></article>'
page=page.replace('__PROJECT_STATUS__',status_section)
(R/'index.html').write_text(page,encoding='utf-8')
# Verify local Markdown/HTML references and source integrity.
errors=[]
for p in R.rglob('*'):
 if p.suffix not in ('.md','.html'):continue
 s=p.read_text(encoding='utf-8')
 links=re.findall(r'\]\(([^)]+)\)',s) if p.suffix=='.md' else re.findall(r'(?:href|src)="([^"]+)"',s)
 for link in links:
  if link.startswith(('http:','https:','#','data:')):continue
  path=unquote(link.split('#')[0])
  if path and not (p.parent/path).exists():errors.append(str(p)+': '+link)
manifest=[] if args.source_only else json.loads((R/'source-manifest.json').read_text())
for row in manifest:
 assert hashlib.sha256((R/row['file']).read_bytes()).hexdigest()==row['sha256']
assert not errors,errors
ids=re.findall(r'\bid="([^"]+)"',page)
assert len(ids)==len(set(ids)), 'Duplicate HTML anchors'
missing_anchors=[a for a in re.findall(r'href="#([^"]+)"',page) if a not in ids]
assert not missing_anchors,missing_anchors
with (R/'inference-register.csv').open(newline='',encoding='utf-8-sig') as f:inferences=list(csv.DictReader(f))
inference_ids=[row['inference_id'] for row in inferences]
assert len(inference_ids)==len(set(inference_ids)), 'Duplicate inference IDs'
referenced=set(re.findall(r'EXT\d{3}','\n'.join((R/n).read_text(encoding='utf-8') for n in docs)))
assert referenced <= set(inference_ids),referenced-set(inference_ids)
json.loads((R/'execution-state.json').read_text(encoding='utf-8'))
result={'date':'2026-10-19','scope':'Documentation integrity only; source-only snapshots explicitly omit local reference evidence. UI inventory and preparation checks are recorded in Audit32. No live acceptance implied.','source_only':args.source_only,'project_count':len(projects),'document_count':len(docs),'inference_count':len(inferences),'source_hashes_verified':len(manifest),'broken_local_links':len(errors),'missing_html_anchors':len(missing_anchors),'duplicate_html_anchors':False,'word_count':sum(len((R/n).read_text(encoding='utf-8').split()) for n in docs),'html_images_referenced':len(re.findall(r'<img\b',page)),'browser_visual_check':'Not executed by this generator; see separate evidence if present'}
(R/'package-validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
