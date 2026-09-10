import json,pathlib,hashlib,re
from pypdf import PdfReader
O=pathlib.Path(__file__).resolve().parent; R=O.parents[1]
d=json.loads((O/'dados.json').read_text(encoding='utf-8'))
for f in d['achados']:
 for l in f['locais']:
  assert 1<=l['inicio']<=l['fim']<=len((R/l['arquivo']).read_text(encoding='utf-8-sig').splitlines()),l
pdf=O/'relatorio-auditoria-seguranca.pdf'; rd=PdfReader(pdf)
txt='\n'.join(p.extract_text() for p in rd.pages)
key=re.search(r'secretKey = "([^"]+)";', (R/'src/EscolaAtenta.API/Program.cs').read_text(encoding='utf-8-sig')).group(1)
assert key not in txt
assert len(rd.pages)==21 and len(d['rotas'])==28
q=json.loads((O/'verificacao-pdf.json').read_text(encoding='utf-8'))
q['visual']='21 páginas rasterizadas com Poppler; inspeção de contato após correção de três quebras órfãs; gráficos, tabelas e oito issues presentes, sem cortes observados.'
q['linhas_evidencias']='Todas as faixas existem nos arquivos da revisão auditada.'
q['redacao_segredos']='Chave de desenvolvimento ausente do texto extraído.'
q['sha256_pdf']=hashlib.sha256(pdf.read_bytes()).hexdigest()
(O/'verificacao-pdf.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
manifest=[]
for p in sorted(O.rglob('*')):
 if not p.is_file() or any(x in {'preview','bin','obj','__pycache__'} for x in p.relative_to(O).parts) or p.name=='manifesto.json': continue
 manifest.append({'arquivo':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(O/'manifesto.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'paginas':len(rd.pages),'arquivos_entregaveis':len(manifest)+1,'achados':len(d['achados']),'rotas':len(d['rotas']),'segredos_redigidos':True}))
